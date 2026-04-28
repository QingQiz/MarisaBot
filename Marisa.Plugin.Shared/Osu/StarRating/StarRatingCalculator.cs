using System.Globalization;

namespace Marisa.Plugin.Shared.Osu;

public static partial class StarRatingCalculator
{
    private const double StarRatingMultiplier = 0.0265;
    private const double PerformanceBaseMultiplier = 1.14;

    public static double Calculate(string beatmapText)
        => Calculate(beatmapText, Array.Empty<string>());

    public static double Calculate(string beatmapText, params string[] mods)
    {
        CalculationMods calculationMods = ParseMods(mods);
        Beatmap beatmap = BeatmapParser.Parse(beatmapText);
        return beatmap.Mode switch
        {
            1 => new TaikoDifficultyCalculator(beatmap, calculationMods.ClockRate).CalculateStarRating(),
            2 => new CatchDifficultyCalculator(beatmap, calculationMods.ClockRate).CalculateStarRating(),
            3 => new ManiaDifficultyCalculator(beatmap, calculationMods.ClockRate).CalculateStarRating(),
            _ => new DifficultyCalculator(beatmap, calculationMods.ClockRate).CalculateStarRating()
        };
    }

    private readonly record struct CalculationMods(double ClockRate);

    private static CalculationMods ParseMods(IEnumerable<string> mods)
    {
        double clockRate = 1;

        foreach (ParsedMod mod in ExpandMods(mods))
        {
            switch (mod.Acronym)
            {
                case "NM":
                case "CL":
                    if (mod.SpeedChange != null)
                        throw new NotSupportedException($"Mod '{mod.Acronym}' does not support speed-change settings.");
                    break;

                case "DT":
                case "NC":
                    clockRate *= ValidateRateModSpeed(mod, 1.5, 1.01, 2.0);
                    break;

                case "HT":
                case "DC":
                    clockRate *= ValidateRateModSpeed(mod, 0.75, 0.5, 0.99);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported mod '{mod.Acronym}'. Supported mods: DT, NC, HT, DC, CL.");
            }
        }

        return new CalculationMods(clockRate);
    }

    private readonly record struct ParsedMod(string Acronym, double? SpeedChange);

    private static double ValidateRateModSpeed(ParsedMod mod, double defaultSpeed, double minSpeed, double maxSpeed)
    {
        double speed = mod.SpeedChange ?? defaultSpeed;

        if (speed < minSpeed || speed > maxSpeed)
            throw new NotSupportedException($"Mod '{mod.Acronym}' speed change must be between {minSpeed.ToString(CultureInfo.InvariantCulture)}x and {maxSpeed.ToString(CultureInfo.InvariantCulture)}x.");

        return speed;
    }

    private static IEnumerable<ParsedMod> ExpandMods(IEnumerable<string> mods)
    {
        foreach (string rawMod in mods)
        {
            foreach (string token in rawMod.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string upper = token.ToUpperInvariant();
                ParsedMod? configuredMod = TryParseConfiguredMod(upper);
                if (configuredMod != null)
                {
                    yield return configuredMod.Value;
                    continue;
                }

                if (upper.Length <= 2)
                {
                    yield return new ParsedMod(upper, null);
                    continue;
                }

                if (upper.Length % 2 != 0)
                    throw new NotSupportedException($"Unsupported mod string '{token}'. Use two-letter mod acronyms, or rate settings like DT=1.25.");

                for (int i = 0; i < upper.Length; i += 2)
                    yield return new ParsedMod(upper.Substring(i, 2), null);
            }
        }
    }

    private static ParsedMod? TryParseConfiguredMod(string token)
    {
        int separator = token.IndexOfAny(new[] { '=', ':', '@' });
        if (separator >= 0)
        {
            if (separator != 2)
                throw new NotSupportedException($"Unsupported mod setting '{token}'. Use syntax like DT=1.25.");

            return new ParsedMod(token[..2], ParseSpeedChange(token[(separator + 1)..], token));
        }

        if (token.Length > 2 && IsRateMod(token[..2]) && double.TryParse(token[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out double speedChange))
            return new ParsedMod(token[..2], speedChange);

        return null;
    }

    private static double ParseSpeedChange(string value, string token)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speedChange))
            throw new NotSupportedException($"Invalid speed-change setting in '{token}'. Use syntax like DT=1.25.");

        return speedChange;
    }

    private static bool IsRateMod(string acronym) => acronym is "DT" or "NC" or "HT" or "DC";

    private sealed class DifficultyCalculator
    {
        private readonly Beatmap beatmap;
        private readonly double clockRate;

        public DifficultyCalculator(Beatmap beatmap, double clockRate)
        {
            this.beatmap = beatmap;
            this.clockRate = clockRate;
        }

        public double CalculateStarRating()
        {
            if (beatmap.HitObjects.Count == 0)
                return 0;

            ApplyStacking(beatmap);

            var difficultyObjects = new List<OsuDifficultyHitObject>();

            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                difficultyObjects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], difficultyObjects, difficultyObjects.Count, beatmap.Difficulty, clockRate));
            }

            var aim = new AimSkill(includeSliders: true);
            var aimWithoutSliders = new AimSkill(includeSliders: false);
            var speed = new SpeedSkill();

            foreach (OsuDifficultyHitObject difficultyObject in difficultyObjects)
            {
                aim.Process(difficultyObject);
                aimWithoutSliders.Process(difficultyObject);
                speed.Process(difficultyObject);
            }

            double aimDifficultyValue = aim.DifficultyValue();
            double aimNoSlidersDifficultyValue = aimWithoutSliders.DifficultyValue();
            double speedDifficultyValue = speed.DifficultyValue();

            double mechanicalDifficultyRating = CalculateMechanicalDifficultyRating(aimDifficultyValue, speedDifficultyValue);
            double sliderFactor = aimDifficultyValue > 0
                ? CalculateDifficultyRating(aimNoSlidersDifficultyValue) / CalculateDifficultyRating(aimDifficultyValue)
                : 1;

            var ratingCalculator = new OsuRatingCalculator(
                beatmap.HitObjects.Count,
                CalculateRateAdjustedApproachRate(beatmap.Difficulty.ApproachRate, clockRate),
                CalculateRateAdjustedOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate),
                mechanicalDifficultyRating,
                sliderFactor);

            double aimRating = ratingCalculator.ComputeAimRating(aimDifficultyValue);
            double speedRating = ratingCalculator.ComputeSpeedRating(speedDifficultyValue);

            double baseAimPerformance = OsuStrainSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = OsuStrainSkill.DifficultyToPerformance(speedRating);
            double basePerformance = Math.Pow(Math.Pow(baseAimPerformance, 1.1) + Math.Pow(baseSpeedPerformance, 1.1), 1.0 / 1.1);

            return CalculateStarRatingFromPerformance(basePerformance);
        }

        private static double CalculateMechanicalDifficultyRating(double aimDifficultyValue, double speedDifficultyValue)
        {
            double aimValue = OsuStrainSkill.DifficultyToPerformance(CalculateDifficultyRating(aimDifficultyValue));
            double speedValue = OsuStrainSkill.DifficultyToPerformance(CalculateDifficultyRating(speedDifficultyValue));
            double totalValue = Math.Pow(Math.Pow(aimValue, 1.1) + Math.Pow(speedValue, 1.1), 1.0 / 1.1);

            return CalculateStarRatingFromPerformance(totalValue);
        }

        private static double CalculateStarRatingFromPerformance(double basePerformance)
        {
            if (basePerformance <= 0.00001)
                return 0;

            return Cbrt(PerformanceBaseMultiplier) * StarRatingMultiplier * (Cbrt(100000 / Math.Pow(2, 1.0 / 1.1) * basePerformance) + 4);
        }
    }

    private sealed class OsuRatingCalculator
    {
        private readonly int totalHits;
        private readonly double approachRate;
        private readonly double overallDifficulty;

        public OsuRatingCalculator(int totalHits, double approachRate, double overallDifficulty, double mechanicalDifficultyRating, double sliderFactor)
        {
            this.totalHits = totalHits;
            this.approachRate = approachRate;
            this.overallDifficulty = overallDifficulty;
        }

        public double ComputeAimRating(double aimDifficultyValue)
        {
            double aimRating = CalculateDifficultyRating(aimDifficultyValue);
            double ratingMultiplier = 1.0;

            double approachRateLengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) + (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);

            double approachRateFactor = 0.0;
            if (approachRate > 10.33)
                approachRateFactor = 0.3 * (approachRate - 10.33);
            else if (approachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - approachRate);

            ratingMultiplier += approachRateFactor * approachRateLengthBonus;
            ratingMultiplier *= 0.98 + Math.Pow(Math.Max(0, overallDifficulty), 2) / 2500;

            return aimRating * Cbrt(ratingMultiplier);
        }

        public double ComputeSpeedRating(double speedDifficultyValue)
        {
            double speedRating = CalculateDifficultyRating(speedDifficultyValue);
            double ratingMultiplier = 1.0;

            double approachRateLengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) + (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);

            double approachRateFactor = 0.0;
            if (approachRate > 10.33)
                approachRateFactor = 0.3 * (approachRate - 10.33);

            ratingMultiplier += approachRateFactor * approachRateLengthBonus;
            ratingMultiplier *= 0.95 + Math.Pow(Math.Max(0, overallDifficulty), 2) / 750;

            return speedRating * Cbrt(ratingMultiplier);
        }
    }

    private abstract class StrainSkill
    {
        private const double DecayWeight = 0.9;
        private const int SectionLength = 400;

        private double currentSectionPeak;
        private double currentSectionEnd;
        private readonly List<double> strainPeaks = new();

        protected readonly List<double> ObjectStrains = new();

        public void Process(OsuDifficultyHitObject current)
        {
            if (current.Index == 0)
                currentSectionEnd = Math.Ceiling(current.StartTime / SectionLength) * SectionLength;

            while (current.StartTime > currentSectionEnd)
            {
                strainPeaks.Add(currentSectionPeak);
                currentSectionPeak = CalculateInitialStrain(currentSectionEnd, current);
                currentSectionEnd += SectionLength;
            }

            double strain = StrainValueAt(current);
            currentSectionPeak = Math.Max(strain, currentSectionPeak);
            ObjectStrains.Add(strain);
        }

        protected abstract double StrainValueAt(OsuDifficultyHitObject current);

        protected abstract double CalculateInitialStrain(double time, OsuDifficultyHitObject current);

        protected IEnumerable<double> CurrentStrainPeaks() => strainPeaks.Append(currentSectionPeak);

        public virtual double DifficultyValue()
        {
            double difficulty = 0;
            double weight = 1;

            foreach (double strain in CurrentStrainPeaks().Where(p => p > 0).OrderByDescending(p => p))
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            return difficulty;
        }
    }

    private abstract class OsuStrainSkill : StrainSkill
    {
        protected virtual int ReducedSectionCount => 10;
        protected virtual double ReducedStrainBaseline => 0.75;

        public override double DifficultyValue()
        {
            double difficulty = 0;
            double weight = 1;
            List<double> strains = CurrentStrainPeaks().Where(p => p > 0).OrderByDescending(p => p).ToList();

            for (int i = 0; i < Math.Min(strains.Count, ReducedSectionCount); i++)
            {
                double scale = Math.Log10(Lerp(1, 10, Math.Clamp((double)i / ReducedSectionCount, 0, 1)));
                strains[i] *= Lerp(ReducedStrainBaseline, 1.0, scale);
            }

            foreach (double strain in strains.OrderByDescending(s => s))
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            return difficulty;
        }

        public static double DifficultyToPerformance(double difficulty) => Math.Pow(5.0 * Math.Max(1.0, difficulty / 0.0675) - 4.0, 3.0) / 100000.0;
    }

    private sealed class AimSkill : OsuStrainSkill
    {
        private readonly bool includeSliders;
        private double currentStrain;

        public AimSkill(bool includeSliders)
        {
            this.includeSliders = includeSliders;
        }

        protected override double CalculateInitialStrain(double time, OsuDifficultyHitObject current) => currentStrain * StrainDecay(time - current.Previous(0)!.StartTime);

        protected override double StrainValueAt(OsuDifficultyHitObject current)
        {
            currentStrain *= StrainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, includeSliders) * 26;
            return currentStrain;
        }

        private static double StrainDecay(double ms) => Math.Pow(0.15, ms / 1000);
    }

    private sealed class SpeedSkill : OsuStrainSkill
    {
        protected override int ReducedSectionCount => 5;

        private double currentStrain;
        private double currentRhythm;

        protected override double CalculateInitialStrain(double time, OsuDifficultyHitObject current) => currentStrain * currentRhythm * StrainDecay(time - current.Previous(0)!.StartTime);

        protected override double StrainValueAt(OsuDifficultyHitObject current)
        {
            currentStrain *= StrainDecay(current.AdjustedDeltaTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current) * 1.47;
            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            return currentStrain * currentRhythm;
        }

        private static double StrainDecay(double ms) => Math.Pow(0.3, ms / 1000);
    }

    private sealed class OsuDifficultyHitObject
    {
        public const int NormalisedRadius = 50;
        public const int NormalisedDiameter = NormalisedRadius * 2;
        public const int MinDeltaTime = 25;

        private const float MaximumSliderRadius = NormalisedRadius * 2.4f;
        private const float AssumedSliderRadius = NormalisedRadius * 1.8f;

        private readonly IReadOnlyList<OsuDifficultyHitObject> difficultyObjects;

        public readonly OsuHitObject BaseObject;
        public readonly OsuHitObject LastObject;
        public readonly int Index;
        public readonly double DeltaTime;
        public readonly double StartTime;
        public readonly double AdjustedDeltaTime;

        public double LazyJumpDistance { get; private set; }
        public double MinimumJumpDistance { get; private set; }
        public double MinimumJumpTime { get; private set; }
        public double TravelDistance { get; private set; }
        public double TravelTime { get; private set; }
        public Vector2? LazyEndPosition { get; private set; }
        public double LazyTravelDistance { get; private set; }
        public double LazyTravelTime { get; private set; }
        public double? Angle { get; private set; }
        public double HitWindowGreat { get; }
        public double SmallCircleBonus { get; }

        public OsuDifficultyHitObject(OsuHitObject hitObject, OsuHitObject lastObject, List<OsuDifficultyHitObject> objects, int index, BeatmapDifficulty difficulty, double clockRate)
        {
            difficultyObjects = objects;
            BaseObject = hitObject;
            LastObject = lastObject;
            Index = index;
            DeltaTime = (hitObject.StartTime - lastObject.StartTime) / clockRate;
            StartTime = hitObject.StartTime / clockRate;
            AdjustedDeltaTime = Math.Max(DeltaTime, MinDeltaTime);
            SmallCircleBonus = Math.Max(1.0, 1.0 + (30 - BaseObject.Radius) / 40);
            HitWindowGreat = 2 * GreatHitWindow(difficulty.OverallDifficulty) / clockRate;

            ComputeSliderCursorPosition();
            SetDistances(clockRate);
        }

        public OsuDifficultyHitObject? Previous(int backwardsIndex)
        {
            int index = Index - (backwardsIndex + 1);
            return index >= 0 && index < difficultyObjects.Count ? difficultyObjects[index] : null;
        }

        public OsuDifficultyHitObject? Next(int forwardsIndex)
        {
            int index = Index + (forwardsIndex + 1);
            return index >= 0 && index < difficultyObjects.Count ? difficultyObjects[index] : null;
        }

        public double GetDoubletapness(OsuDifficultyHitObject? nextObject)
        {
            if (nextObject == null)
                return 0;

            double currDeltaTime = Math.Max(1, DeltaTime);
            double nextDeltaTime = Math.Max(1, nextObject.DeltaTime);
            double deltaDifference = Math.Abs(nextDeltaTime - currDeltaTime);
            double speedRatio = currDeltaTime / Math.Max(currDeltaTime, deltaDifference);
            double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / HitWindowGreat), 2);
            return 1.0 - Math.Pow(speedRatio, 1 - windowRatio);
        }

        private void SetDistances(double clockRate)
        {
            if (BaseObject is Slider currentSlider)
            {
                TravelDistance = LazyTravelDistance * Math.Pow(1 + currentSlider.RepeatCount / 2.5, 1.0 / 2.5);
                TravelTime = Math.Max(LazyTravelTime / clockRate, MinDeltaTime);
            }

            if (BaseObject is Spinner || LastObject is Spinner)
                return;

            float scalingFactor = NormalisedRadius / (float)BaseObject.Radius;
            Vector2 lastCursorPosition = Previous(0) != null ? GetEndCursorPosition(Previous(0)!) : LastObject.StackedPosition;

            LazyJumpDistance = (BaseObject.StackedPosition * scalingFactor - lastCursorPosition * scalingFactor).Length;
            MinimumJumpTime = AdjustedDeltaTime;
            MinimumJumpDistance = LazyJumpDistance;

            if (LastObject is Slider lastSlider && Previous(0) != null)
            {
                double lastTravelTime = Math.Max(Previous(0)!.LazyTravelTime / clockRate, MinDeltaTime);
                MinimumJumpTime = Math.Max(AdjustedDeltaTime - lastTravelTime, MinDeltaTime);
                float tailJumpDistance = (lastSlider.TailPosition - BaseObject.StackedPosition).Length * scalingFactor;
                MinimumJumpDistance = Math.Max(0, Math.Min(LazyJumpDistance - (MaximumSliderRadius - AssumedSliderRadius), tailJumpDistance - MaximumSliderRadius));
            }

            if (Previous(1) != null && Previous(1)!.BaseObject is not Spinner)
            {
                Vector2 lastLastCursorPosition = GetEndCursorPosition(Previous(1)!);
                Vector2 v1 = lastLastCursorPosition - LastObject.StackedPosition;
                Vector2 v2 = BaseObject.StackedPosition - lastCursorPosition;
                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;
                Angle = Math.Abs(Math.Atan2(det, dot));
            }
        }

        private void ComputeSliderCursorPosition()
        {
            if (BaseObject is not Slider slider)
                return;

            double trackingEndTime = Math.Max(slider.StartTime + slider.Duration - 36, slider.StartTime + slider.Duration / 2);
            List<NestedSliderObject> nestedObjects = slider.NestedObjects;
            NestedSliderObject? lastRealTick = nestedObjects.LastOrDefault(n => n.Type == NestedSliderObjectType.Tick);

            if (lastRealTick != null && lastRealTick.StartTime > trackingEndTime)
            {
                trackingEndTime = lastRealTick.StartTime;
                nestedObjects = nestedObjects.Where(n => !ReferenceEquals(n, lastRealTick)).Append(lastRealTick).ToList();
            }

            LazyTravelTime = trackingEndTime - slider.StartTime;

            double endTimeMin = LazyTravelTime / slider.SpanDuration;
            if (endTimeMin % 2 >= 1)
                endTimeMin = 1 - endTimeMin % 1;
            else
                endTimeMin %= 1;

            LazyEndPosition = slider.StackedPosition + slider.Path.PositionAt(endTimeMin);
            Vector2 currCursorPosition = slider.StackedPosition;
            double scalingFactor = NormalisedRadius / slider.Radius;

            for (int i = 1; i < nestedObjects.Count; i++)
            {
                NestedSliderObject currMovementObject = nestedObjects[i];
                Vector2 currMovement = currMovementObject.StackedPosition - currCursorPosition;
                double currMovementLength = scalingFactor * currMovement.Length;
                double requiredMovement = AssumedSliderRadius;

                if (i == nestedObjects.Count - 1)
                {
                    Vector2 lazyMovement = LazyEndPosition.Value - currCursorPosition;
                    if (lazyMovement.Length < currMovement.Length)
                        currMovement = lazyMovement;

                    currMovementLength = scalingFactor * currMovement.Length;
                }
                else if (currMovementObject.Type == NestedSliderObjectType.Repeat)
                {
                    requiredMovement = NormalisedRadius;
                }

                if (currMovementLength > requiredMovement)
                {
                    currCursorPosition += currMovement * (float)((currMovementLength - requiredMovement) / currMovementLength);
                    currMovementLength *= (currMovementLength - requiredMovement) / currMovementLength;
                    LazyTravelDistance += currMovementLength;
                }

                if (i == nestedObjects.Count - 1)
                    LazyEndPosition = currCursorPosition;
            }
        }

        private static Vector2 GetEndCursorPosition(OsuDifficultyHitObject difficultyObject) => difficultyObject.LazyEndPosition ?? difficultyObject.BaseObject.StackedPosition;
    }

    private static class AimEvaluator
    {
        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0)!.BaseObject is Spinner)
                return 0;

            OsuDifficultyHitObject last = current.Previous(0)!;
            OsuDifficultyHitObject lastLast = current.Previous(1)!;
            OsuDifficultyHitObject? last2 = current.Previous(2);

            double currVelocity = current.LazyJumpDistance / current.AdjustedDeltaTime;

            if (last.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = last.TravelDistance / last.TravelTime;
                double movementVelocity = current.MinimumJumpDistance / current.MinimumJumpTime;
                currVelocity = Math.Max(currVelocity, movementVelocity + travelVelocity);
            }

            double prevVelocity = last.LazyJumpDistance / last.AdjustedDeltaTime;

            if (lastLast.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = lastLast.TravelDistance / lastLast.TravelTime;
                double movementVelocity = last.MinimumJumpDistance / last.MinimumJumpTime;
                prevVelocity = Math.Max(prevVelocity, movementVelocity + travelVelocity);
            }

            double wideAngleBonus = 0;
            double acuteAngleBonus = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;
            double aimStrain = currVelocity;

            if (current.Angle != null && last.Angle != null)
            {
                double currAngle = current.Angle.Value;
                double lastAngle = last.Angle.Value;
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                if (Math.Max(current.AdjustedDeltaTime, last.AdjustedDeltaTime) < 1.25 * Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime))
                {
                    acuteAngleBonus = CalcAcuteAngleBonus(currAngle);
                    acuteAngleBonus *= 0.08 + 0.92 * (1 - Math.Min(acuteAngleBonus, Math.Pow(CalcAcuteAngleBonus(lastAngle), 3)));
                    acuteAngleBonus *= angleBonus * Smootherstep(MillisecondsToBpm(current.AdjustedDeltaTime, 2), 300, 400) * Smootherstep(current.LazyJumpDistance, OsuDifficultyHitObject.NormalisedDiameter, OsuDifficultyHitObject.NormalisedDiameter * 2);
                }

                wideAngleBonus = CalcWideAngleBonus(currAngle);
                wideAngleBonus *= 1 - Math.Min(wideAngleBonus, Math.Pow(CalcWideAngleBonus(lastAngle), 3));
                wideAngleBonus *= angleBonus * Smootherstep(current.LazyJumpDistance, 0, OsuDifficultyHitObject.NormalisedDiameter);

                wiggleBonus = angleBonus
                              * Smootherstep(current.LazyJumpDistance, OsuDifficultyHitObject.NormalisedRadius, OsuDifficultyHitObject.NormalisedDiameter)
                              * Math.Pow(ReverseLerp(current.LazyJumpDistance, OsuDifficultyHitObject.NormalisedDiameter * 3, OsuDifficultyHitObject.NormalisedDiameter), 1.8)
                              * Smootherstep(currAngle, DegreesToRadians(110), DegreesToRadians(60))
                              * Smootherstep(last.LazyJumpDistance, OsuDifficultyHitObject.NormalisedRadius, OsuDifficultyHitObject.NormalisedDiameter)
                              * Math.Pow(ReverseLerp(last.LazyJumpDistance, OsuDifficultyHitObject.NormalisedDiameter * 3, OsuDifficultyHitObject.NormalisedDiameter), 1.8)
                              * Smootherstep(lastAngle, DegreesToRadians(110), DegreesToRadians(60));

                if (last2 != null)
                {
                    float distance = (last2.BaseObject.StackedPosition - last.BaseObject.StackedPosition).Length;
                    if (distance < 1)
                        wideAngleBonus *= 1 - 0.35 * (1 - distance);
                }
            }

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                prevVelocity = (last.LazyJumpDistance + lastLast.TravelDistance) / last.AdjustedDeltaTime;
                currVelocity = (current.LazyJumpDistance + last.TravelDistance) / current.AdjustedDeltaTime;

                double distRatio = Smoothstep(Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity), 0, 1);
                double overlapVelocityBuff = Math.Min(OsuDifficultyHitObject.NormalisedDiameter * 1.25 / Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime), Math.Abs(prevVelocity - currVelocity));
                velocityChangeBonus = overlapVelocityBuff * distRatio;
                velocityChangeBonus *= Math.Pow(Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime) / Math.Max(current.AdjustedDeltaTime, last.AdjustedDeltaTime), 2);
            }

            if (last.BaseObject is Slider)
                sliderBonus = last.TravelDistance / last.TravelTime;

            aimStrain += wiggleBonus * 1.02;
            aimStrain += velocityChangeBonus * 0.75;
            aimStrain += Math.Max(acuteAngleBonus * 2.55, wideAngleBonus * 1.5);
            aimStrain *= current.SmallCircleBonus;

            if (withSliderTravelDistance)
                aimStrain += sliderBonus * 1.35;

            return aimStrain;
        }

        private static double CalcWideAngleBonus(double angle) => Smoothstep(angle, DegreesToRadians(40), DegreesToRadians(140));

        private static double CalcAcuteAngleBonus(double angle) => Smoothstep(angle, DegreesToRadians(140), DegreesToRadians(40));
    }

    private static class SpeedEvaluator
    {
        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double strainTime = current.AdjustedDeltaTime;
            double doubletapness = 1.0 - current.GetDoubletapness(current.Next(0));
            strainTime /= Math.Clamp((strainTime / current.HitWindowGreat) / 0.93, 0.92, 1);

            double speedBonus = 0.0;
            if (MillisecondsToBpm(strainTime) > 200)
                speedBonus = 0.75 * Math.Pow((BpmToMilliseconds(200) - strainTime) / 40, 2);

            double travelDistance = current.Previous(0)?.TravelDistance ?? 0;
            double distance = Math.Min(travelDistance + current.MinimumJumpDistance, OsuDifficultyHitObject.NormalisedDiameter * 1.25);
            double distanceBonus = Math.Pow(distance / (OsuDifficultyHitObject.NormalisedDiameter * 1.25), 3.95) * 0.8;
            distanceBonus *= Math.Sqrt(current.SmallCircleBonus);

            return (1 + speedBonus + distanceBonus) * 1000 / strainTime * doubletapness;
        }
    }

    private static class RhythmEvaluator
    {
        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double rhythmComplexitySum = 0;
            double deltaDifferenceEpsilon = current.HitWindowGreat * 0.3;
            var island = new Island(deltaDifferenceEpsilon);
            var previousIsland = new Island(deltaDifferenceEpsilon);
            var islandCounts = new List<(Island Island, int Count)>();
            double startRatio = 0;
            bool firstDeltaSwitch = false;
            int historicalNoteCount = Math.Min(current.Index, 32);
            int rhythmStart = 0;

            while (rhythmStart < historicalNoteCount - 2 && current.StartTime - current.Previous(rhythmStart)!.StartTime < 5000)
                rhythmStart++;

            OsuDifficultyHitObject prevObj = current.Previous(rhythmStart)!;
            OsuDifficultyHitObject lastObj = current.Previous(rhythmStart + 1)!;

            for (int i = rhythmStart; i > 0; i--)
            {
                OsuDifficultyHitObject currObj = current.Previous(i - 1)!;
                double timeDecay = (5000 - (current.StartTime - currObj.StartTime)) / 5000;
                double noteDecay = (double)(historicalNoteCount - i) / historicalNoteCount;
                double currHistoricalDecay = Math.Min(noteDecay, timeDecay);
                double currDelta = Math.Max(currObj.DeltaTime, 1e-7);
                double prevDelta = Math.Max(prevObj.DeltaTime, 1e-7);
                double lastDelta = Math.Max(lastObj.DeltaTime, 1e-7);
                double deltaDifference = Math.Max(prevDelta, currDelta) / Math.Min(prevDelta, currDelta);
                double deltaDifferenceFraction = deltaDifference - Math.Truncate(deltaDifference);
                double currRatio = 1.0 + 15.0 * Math.Min(0.5, SmoothstepBellCurve(deltaDifferenceFraction));
                double differenceMultiplier = Math.Clamp(2.0 - deltaDifference / 8.0, 0.0, 1.0);
                double windowPenalty = Math.Min(1, Math.Max(0, Math.Abs(prevDelta - currDelta) - deltaDifferenceEpsilon) / deltaDifferenceEpsilon);
                double effectiveRatio = windowPenalty * currRatio * differenceMultiplier;

                if (firstDeltaSwitch)
                {
                    if (Math.Abs(prevDelta - currDelta) < deltaDifferenceEpsilon)
                    {
                        island.AddDelta((int)currDelta);
                    }
                    else
                    {
                        if (currObj.BaseObject is Slider)
                            effectiveRatio *= 0.125;

                        if (prevObj.BaseObject is Slider)
                            effectiveRatio *= 0.3;

                        if (island.IsSimilarPolarity(previousIsland))
                            effectiveRatio *= 0.5;

                        if (lastDelta > prevDelta + deltaDifferenceEpsilon && prevDelta > currDelta + deltaDifferenceEpsilon)
                            effectiveRatio *= 0.125;

                        if (previousIsland.DeltaCount == island.DeltaCount)
                            effectiveRatio *= 0.5;

                        int islandIndex = islandCounts.FindIndex(x => x.Island.Equals(island));

                        if (islandIndex >= 0)
                        {
                            (Island islandValue, int count) = islandCounts[islandIndex];

                            if (previousIsland.Equals(island))
                                count++;

                            double power = Logistic(island.Delta, maxValue: 2.75, multiplier: 0.24, midpointOffset: 58.33);
                            effectiveRatio *= Math.Min(3.0 / count, Math.Pow(1.0 / count, power));
                            islandCounts[islandIndex] = (islandValue, count);
                        }
                        else
                        {
                            islandCounts.Add((island, 1));
                        }

                        double doubletapness = prevObj.GetDoubletapness(currObj);
                        effectiveRatio *= 1 - doubletapness * 0.75;
                        rhythmComplexitySum += Math.Sqrt(effectiveRatio * startRatio) * currHistoricalDecay;
                        startRatio = effectiveRatio;
                        previousIsland = island;

                        if (prevDelta + deltaDifferenceEpsilon < currDelta)
                            firstDeltaSwitch = false;

                        island = new Island((int)currDelta, deltaDifferenceEpsilon);
                    }
                }
                else if (prevDelta > currDelta + deltaDifferenceEpsilon)
                {
                    firstDeltaSwitch = true;

                    if (currObj.BaseObject is Slider)
                        effectiveRatio *= 0.6;

                    if (prevObj.BaseObject is Slider)
                        effectiveRatio *= 0.6;

                    startRatio = effectiveRatio;
                    island = new Island((int)currDelta, deltaDifferenceEpsilon);
                }

                lastObj = prevObj;
                prevObj = currObj;
            }

            double rhythmDifficulty = Math.Sqrt(4 + rhythmComplexitySum) / 2.0;
            rhythmDifficulty *= 1 - current.GetDoubletapness(current.Next(0));
            return rhythmDifficulty;
        }

        private sealed class Island : IEquatable<Island>
        {
            private readonly double epsilon;

            public Island(double epsilon)
            {
                this.epsilon = epsilon;
            }

            public Island(int delta, double epsilon)
            {
                this.epsilon = epsilon;
                Delta = Math.Max(delta, OsuDifficultyHitObject.MinDeltaTime);
                DeltaCount++;
            }

            public int Delta { get; private set; } = int.MaxValue;
            public int DeltaCount { get; private set; }

            public void AddDelta(int delta)
            {
                if (Delta == int.MaxValue)
                    Delta = Math.Max(delta, OsuDifficultyHitObject.MinDeltaTime);

                DeltaCount++;
            }

            public bool IsSimilarPolarity(Island other) => DeltaCount % 2 == other.DeltaCount % 2;

            public bool Equals(Island? other) => other != null && Math.Abs(Delta - other.Delta) < epsilon && DeltaCount == other.DeltaCount;
        }
    }

    private static class BeatmapParser
    {
        public static Beatmap Parse(string text)
        {
            var beatmap = new Beatmap();
            string section = string.Empty;
            int formatVersion = 14;
            bool hasApproachRate = false;

            foreach (string rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("osu file format v", StringComparison.OrdinalIgnoreCase))
                {
                    formatVersion = ParseInt(line[17..]);
                    beatmap.FormatVersion = formatVersion;
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1];
                    continue;
                }

                switch (section)
                {
                    case "General":
                        ParseGeneral(line, beatmap);
                        break;

                    case "Difficulty":
                        ParseDifficulty(line, beatmap.Difficulty, ref hasApproachRate);
                        break;

                    case "TimingPoints":
                        ParseTimingPoint(line, beatmap);
                        break;

                    case "HitObjects":
                        ParseHitObject(line, beatmap, formatVersion);
                        break;
                }
            }

            if (!hasApproachRate)
                beatmap.Difficulty.ApproachRate = beatmap.Difficulty.OverallDifficulty;

            beatmap.Difficulty.DrainRate = Math.Clamp(beatmap.Difficulty.DrainRate, 0, 10);
            beatmap.Difficulty.CircleSize = Math.Clamp(beatmap.Difficulty.CircleSize, 0, 10);
            beatmap.Difficulty.OverallDifficulty = Math.Clamp(beatmap.Difficulty.OverallDifficulty, 0, 10);
            beatmap.Difficulty.ApproachRate = Math.Clamp(beatmap.Difficulty.ApproachRate, 0, 10);
            beatmap.Difficulty.SliderMultiplier = Math.Clamp(beatmap.Difficulty.SliderMultiplier, 0.4, 3.6);
            beatmap.Difficulty.SliderTickRate = Math.Clamp(beatmap.Difficulty.SliderTickRate, 0.5, 8);
            beatmap.TimingPoints.Sort((a, b) => a.Time.CompareTo(b.Time));
            beatmap.HitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            foreach (OsuHitObject hitObject in beatmap.HitObjects)
                ApplyDefaults(hitObject, beatmap);

            return beatmap;
        }

        private static void ParseGeneral(string line, Beatmap beatmap)
        {
            (string key, string value) = SplitKeyValue(line);
            if (key == "StackLeniency")
                beatmap.StackLeniency = ParseFloat(value);
            else if (key == "Mode")
                beatmap.Mode = ParseInt(value);
        }

        private static void ParseDifficulty(string line, BeatmapDifficulty difficulty, ref bool hasApproachRate)
        {
            (string key, string value) = SplitKeyValue(line);

            switch (key)
            {
                case "HPDrainRate":
                    difficulty.DrainRate = ParseFloat(value);
                    break;

                case "CircleSize":
                    difficulty.CircleSize = ParseFloat(value);
                    break;

                case "OverallDifficulty":
                    difficulty.OverallDifficulty = ParseFloat(value);
                    if (!hasApproachRate)
                        difficulty.ApproachRate = difficulty.OverallDifficulty;
                    break;

                case "ApproachRate":
                    difficulty.ApproachRate = ParseFloat(value);
                    hasApproachRate = true;
                    break;

                case "SliderMultiplier":
                    difficulty.SliderMultiplier = ParseDouble(value);
                    break;

                case "SliderTickRate":
                    difficulty.SliderTickRate = ParseDouble(value);
                    break;
            }
        }

        private static void ParseTimingPoint(string line, Beatmap beatmap)
        {
            string[] split = line.Split(',');
            if (split.Length < 2)
                return;

            double time = ParseDouble(split[0].Trim());
            double beatLength = ParseDouble(split[1].Trim());
            bool timingChange = split.Length < 7 || split[6].Trim().Length == 0 || split[6].Trim()[0] == '1';

            if (timingChange)
            {
                beatmap.TimingPoints.Add(new TimingPoint(time, beatLength, 1, true));
            }
            else
            {
                double sliderVelocity = beatLength < 0 ? 100.0 / -beatLength : 1;
                beatmap.TimingPoints.Add(new TimingPoint(time, 0, sliderVelocity, false));
            }
        }

        private static void ParseHitObject(string line, Beatmap beatmap, int formatVersion)
        {
            string[] split = line.Split(',');
            if (split.Length < 5)
                return;

            double x = ParseDouble(split[0]);
            double y = ParseDouble(split[1]);
            double startTime = ParseDouble(split[2]);
            int type = ParseInt(split[3]);
            var position = new Vector2((float)x, (float)y);

            if (beatmap.Mode == 3)
            {
                double endTime = startTime;
                if ((type & 128) != 0 && split.Length >= 6)
                    endTime = Math.Max(startTime, ParseDouble(split[5].Split(':')[0]));

                int columns = Math.Max(1, (int)Math.Round(beatmap.Difficulty.CircleSize));
                int column = Math.Clamp((int)(x * columns / 512), 0, columns - 1);
                beatmap.HitObjects.Add(new ManiaObject(position, startTime, endTime, column));
                return;
            }

            if (beatmap.Mode == 1)
            {
                if ((type & 1) != 0)
                {
                    int soundType = ParseInt(split[4]);
                    beatmap.HitObjects.Add(new TaikoHit(position, startTime, (soundType & 2) != 0 || (soundType & 8) != 0));
                }
                else if ((type & 2) != 0 && split.Length >= 8)
                {
                    PathControlPoint[] controlPoints = ConvertPathString(split[5], position, formatVersion);
                    int spanCount = Math.Max(1, ParseInt(split[6]));
                    double lengthValue = Math.Max(0, ParseDouble(split[7]));
                    double? length = lengthValue == 0 ? null : lengthValue;
                    beatmap.HitObjects.Add(new TaikoDrumRoll(position, startTime, new SliderPath(controlPoints, length), spanCount - 1));
                }
                else if ((type & 8) != 0 && split.Length >= 6)
                {
                    beatmap.HitObjects.Add(new TaikoDrumRoll(position, startTime, ParseDouble(split[5])));
                }

                return;
            }

            if (beatmap.Mode == 2)
            {
                if ((type & 1) != 0)
                {
                    beatmap.HitObjects.Add(new CatchFruit(position, startTime));
                }
                else if ((type & 2) != 0 && split.Length >= 8)
                {
                    PathControlPoint[] controlPoints = ConvertPathString(split[5], position, formatVersion);
                    int spanCount = Math.Max(1, ParseInt(split[6]));
                    double lengthValue = Math.Max(0, ParseDouble(split[7]));
                    double? length = lengthValue == 0 ? null : lengthValue;
                    beatmap.HitObjects.Add(new CatchJuiceStream(position, startTime, new SliderPath(controlPoints, length), spanCount - 1));
                }

                return;
            }

            if ((type & 1) != 0)
            {
                beatmap.HitObjects.Add(new HitCircle(position, startTime));
            }
            else if ((type & 2) != 0 && split.Length >= 8)
            {
                PathControlPoint[] controlPoints = ConvertPathString(split[5], position, formatVersion);
                int spanCount = Math.Max(1, ParseInt(split[6]));
                double lengthValue = Math.Max(0, ParseDouble(split[7]));
                double? length = lengthValue == 0 ? null : lengthValue;
                var path = new SliderPath(controlPoints, length) { OptimiseCatmull = true };
                beatmap.HitObjects.Add(new Slider(position, startTime, path, spanCount - 1));
            }
            else if ((type & 8) != 0 && split.Length >= 6)
            {
                beatmap.HitObjects.Add(new Spinner(new Vector2(256, 192), startTime, ParseDouble(split[5])));
            }
        }

        private static PathControlPoint[] ConvertPathString(string pointString, Vector2 offset, int formatVersion)
        {
            string[] pointStringSplit = pointString.Split('|');
            var points = new List<Vector2>();
            var segments = new List<(PathType Type, int StartIndex)>();

            foreach (string segment in pointStringSplit)
            {
                if (segment.Length == 0)
                    continue;

                if (char.IsLetter(segment[0]))
                {
                    segments.Add((ConvertPathType(segment[0]), points.Count));
                    if (points.Count == 0)
                        points.Add(Vector2.Zero);
                }
                else
                {
                    points.Add(ReadPoint(segment, offset, formatVersion));
                }
            }

            var result = new List<PathControlPoint>();

            for (int i = 0; i < segments.Count; i++)
            {
                int startIndex = segments[i].StartIndex;
                int endIndex = i < segments.Count - 1 ? segments[i + 1].StartIndex : points.Count;
                Vector2? endPoint = i < segments.Count - 1 ? points[endIndex] : null;
                result.AddRange(ConvertPoints(segments[i].Type, points.GetRange(startIndex, endIndex - startIndex), endPoint, formatVersion));
            }

            return result.ToArray();
        }

        private static IEnumerable<PathControlPoint> ConvertPoints(PathType type, List<Vector2> points, Vector2? endPoint, int formatVersion)
        {
            var vertices = points.Select(p => new PathControlPoint(p)).ToArray();
            if (vertices.Length == 0)
                yield break;

            if (type.Kind == PathKind.PerfectCurve)
            {
                int endPointLength = endPoint == null ? 0 : 1;
                if (formatVersion < 128)
                {
                    if (vertices.Length + endPointLength != 3)
                        type = PathType.Bezier;
                    else if (IsLinear(points[0], points[1], endPoint ?? points[2]))
                        type = PathType.Linear;
                }
                else if (vertices.Length + endPointLength > 3)
                {
                    type = PathType.Bezier;
                }
            }

            vertices[0].Type = type;
            int startIndex = 0;
            int endIndex = 0;

            while (++endIndex < vertices.Length)
            {
                if (vertices[endIndex].Position != vertices[endIndex - 1].Position)
                    continue;

                if (type.Kind == PathKind.Catmull && endIndex > 1 && formatVersion < 128)
                    continue;

                if (endIndex == vertices.Length - 1)
                    continue;

                vertices[endIndex - 1].Type = type;

                for (int i = startIndex; i < endIndex; i++)
                    yield return vertices[i];

                startIndex = endIndex + 1;
            }

            if (startIndex < endIndex)
            {
                for (int i = startIndex; i < endIndex; i++)
                    yield return vertices[i];
            }
        }

        private static Vector2 ReadPoint(string value, Vector2 startPosition, int formatVersion)
        {
            string[] split = value.Split(':');
            float x = ParseFloat(split[0]);
            float y = ParseFloat(split[1]);

            if (formatVersion < 128)
            {
                x = (int)x;
                y = (int)y;
            }

            return new Vector2(x, y) - startPosition;
        }

        private static PathType ConvertPathType(char c) => c switch
        {
            'C' => PathType.Catmull,
            'L' => PathType.Linear,
            'P' => PathType.PerfectCurve,
            _ => PathType.Bezier
        };

        private static bool IsLinear(Vector2 p0, Vector2 p1, Vector2 p2) => AlmostEquals(0, (p1.Y - p0.Y) * (p2.X - p0.X) - (p1.X - p0.X) * (p2.Y - p0.Y));

        private static void ApplyDefaults(OsuHitObject hitObject, Beatmap beatmap)
        {
            hitObject.Scale = CalculateScaleFromCircleSize(beatmap.Difficulty.CircleSize, beatmap.Mode == 0);
            hitObject.TimePreempt = DifficultyRangeInt(beatmap.Difficulty.ApproachRate, 1800, 1200, 450);

            if (hitObject is Slider slider)
            {
                TimingPoint timingPoint = beatmap.TimingPointAt(slider.StartTime);
                double sliderVelocityMultiplier = beatmap.SliderVelocityAt(slider.StartTime);
                slider.Velocity = 100 * beatmap.Difficulty.SliderMultiplier / PrecisionAdjustedBeatLength(sliderVelocityMultiplier, timingPoint.BeatLength, maxSliderVelocity: 1000);
                double scoringDistance = slider.Velocity * timingPoint.BeatLength;
                slider.TickDistance = scoringDistance / beatmap.Difficulty.SliderTickRate;
                slider.BuildNestedObjects();
            }

            if (hitObject is TaikoDrumRoll drumRoll)
            {
                if (drumRoll.Path != null)
                {
                    TimingPoint timingPoint = beatmap.TimingPointAt(drumRoll.StartTime);
                    double beatLength = PrecisionAdjustedBeatLength(beatmap.SliderVelocityAt(drumRoll.StartTime), timingPoint.BeatLength, maxSliderVelocity: 10000);
                    double distance = (drumRoll.Path.ExpectedDistance ?? 0) * 1.4f * (drumRoll.RepeatCount + 1);
                    double taikoVelocity = 100 * beatmap.Difficulty.SliderMultiplier * 1.4f;
                    int taikoDuration = (int)(distance / taikoVelocity * beatLength);
                    drumRoll.EndTimeValue = drumRoll.StartTime + taikoDuration;
                }
            }

            if (hitObject is CatchJuiceStream juiceStream)
            {
                TimingPoint timingPoint = beatmap.TimingPointAt(juiceStream.StartTime);
                double sliderVelocityMultiplier = beatmap.SliderVelocityAt(juiceStream.StartTime);
                juiceStream.Velocity = 100 * beatmap.Difficulty.SliderMultiplier / PrecisionAdjustedBeatLength(sliderVelocityMultiplier, timingPoint.BeatLength, maxSliderVelocity: 1000);
                double scoringDistance = juiceStream.Velocity * timingPoint.BeatLength;
                juiceStream.TickDistance = scoringDistance / beatmap.Difficulty.SliderTickRate;
                juiceStream.BuildNestedObjects();
            }
        }

        private static (string Key, string Value) SplitKeyValue(string line)
        {
            int separator = line.IndexOf(':');
            if (separator < 0)
                return (line.Trim(), string.Empty);

            return (line[..separator].Trim(), line[(separator + 1)..].Trim());
        }
    }

    private sealed class Beatmap
    {
        public int FormatVersion { get; set; } = 14;
        public int Mode { get; set; }
        public float StackLeniency { get; set; } = 0.7f;
        public BeatmapDifficulty Difficulty { get; } = new();
        public List<TimingPoint> TimingPoints { get; } = new() { new TimingPoint(0, 1000, 1, true) };
        public List<OsuHitObject> HitObjects { get; } = new();

        public TimingPoint TimingPointAt(double time)
        {
            TimingPoint current = TimingPoints.First(p => p.TimingChange);
            foreach (TimingPoint point in TimingPoints)
            {
                if (point.Time > time)
                    break;

                if (point.TimingChange)
                    current = point;
            }

            return current;
        }

        public double SliderVelocityAt(double time)
        {
            double sliderVelocity = 1;
            foreach (TimingPoint point in TimingPoints)
            {
                if (point.Time > time)
                    break;

                if (!point.TimingChange)
                    sliderVelocity = point.SliderVelocity;
                else
                    sliderVelocity = 1;
            }

            return sliderVelocity;
        }

        public double ScrollSpeedAt(double time)
        {
            double scrollSpeed = 1;
            foreach (TimingPoint point in TimingPoints)
            {
                if (point.Time > time)
                    break;

                scrollSpeed = point.SliderVelocity;
            }

            return scrollSpeed;
        }
    }

    private sealed class BeatmapDifficulty
    {
        public float DrainRate { get; set; } = 5;
        public float CircleSize { get; set; } = 5;
        public float OverallDifficulty { get; set; } = 5;
        public float ApproachRate { get; set; } = 5;
        public double SliderMultiplier { get; set; } = 1;
        public double SliderTickRate { get; set; } = 1;
    }

    private sealed record TimingPoint(double Time, double BeatLength, double SliderVelocity, bool TimingChange);

    private abstract class OsuHitObject
    {
        protected OsuHitObject(Vector2 position, double startTime)
        {
            Position = position;
            StartTime = startTime;
        }

        public Vector2 Position { get; }
        public double StartTime { get; }
        public int StackHeight { get; set; }
        public float Scale { get; set; } = 1;
        public double TimePreempt { get; set; } = 600;
        public double Radius => 64 * Scale;
        public Vector2 StackOffset => new(StackHeight * Scale * -6.4f, StackHeight * Scale * -6.4f);
        public Vector2 StackedPosition => Position + StackOffset;
        public virtual Vector2 EndPosition => Position;
        public virtual double EndTime => StartTime;
    }

    private sealed class HitCircle : OsuHitObject
    {
        public HitCircle(Vector2 position, double startTime)
            : base(position, startTime)
        {
        }
    }

    private sealed class Spinner : OsuHitObject
    {
        public Spinner(Vector2 position, double startTime, double endTime)
            : base(position, startTime)
        {
            EndTime = endTime;
        }

        public override double EndTime { get; }
    }

    private sealed class Slider : OsuHitObject
    {
        public Slider(Vector2 position, double startTime, SliderPath path, int repeatCount)
            : base(position, startTime)
        {
            Path = path;
            RepeatCount = repeatCount;
        }

        public SliderPath Path { get; }
        public int RepeatCount { get; }
        public double Velocity { get; set; } = 1;
        public double TickDistance { get; set; } = double.PositiveInfinity;
        public double SpanCount => RepeatCount + 1;
        public double SpanDuration => Path.Distance / Velocity;
        public double Duration => SpanCount * Path.Distance / Velocity;
        public override double EndTime => StartTime + Duration;
        public override Vector2 EndPosition => Position + Path.PositionAt(1);
        public Vector2 TailPosition => StackedPosition + Path.PositionAt(RepeatCount % 2 == 0 ? 1 : 0);
        public List<NestedSliderObject> NestedObjects { get; private set; } = new();

        public void BuildNestedObjects()
        {
            NestedObjects = SliderEventGenerator.Generate(StartTime, SpanDuration, Velocity, TickDistance, Path.Distance, (int)SpanCount)
                                                .Where(e => e.Type != NestedSliderObjectType.LegacyLastTick)
                                                .Select(e => new NestedSliderObject(e.Type, e.Time, StackedPosition + Path.PositionAt(e.PathProgress)))
                                                .ToList();
        }
    }

    private sealed class LegacyRandom
    {
        private const double intToReal = 1.0 / (int.MaxValue + 1.0);
        private const uint intMask = 0x7fffffff;
        private const uint initialY = 842502087;
        private const uint initialZ = 3579807591;
        private const uint initialW = 273326509;

        private uint x;
        private uint y = initialY;
        private uint z = initialZ;
        private uint w = initialW;
        private uint bitBuffer;
        private int bitIndex = 32;

        public LegacyRandom(int seed)
        {
            x = (uint)seed;
        }

        public int Next()
        {
            return (int)(intMask & NextUInt());
        }

        public int Next(double lowerBound, double upperBound)
        {
            return (int)(lowerBound + NextDouble() * (upperBound - lowerBound));
        }

        public double NextDouble()
        {
            return intToReal * Next();
        }

        public bool NextBool()
        {
            if (bitIndex == 32)
            {
                bitBuffer = NextUInt();
                bitIndex = 1;
                return (bitBuffer & 1) == 1;
            }

            bitIndex++;
            return ((bitBuffer >>= 1) & 1) == 1;
        }

        private uint NextUInt()
        {
            uint t = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            return w = w ^ (w >> 19) ^ t ^ (t >> 8);
        }
    }

    private sealed record NestedSliderObject(NestedSliderObjectType Type, double StartTime, Vector2 StackedPosition);

    private enum NestedSliderObjectType
    {
        Tick,
        LegacyLastTick,
        Head,
        Tail,
        Repeat
    }

    private static class SliderEventGenerator
    {
        public static IEnumerable<SliderEvent> Generate(double startTime, double spanDuration, double velocity, double tickDistance, double totalDistance, int spanCount)
        {
            const double maxLength = 100000;
            double length = Math.Min(maxLength, totalDistance);
            tickDistance = Math.Clamp(tickDistance, 0, length);
            double minDistanceFromEnd = velocity * 10;

            yield return new SliderEvent(NestedSliderObjectType.Head, startTime, 0, 0, startTime);

            for (int span = 0; span < spanCount; span++)
            {
                double spanStartTime = startTime + span * spanDuration;
                bool reversed = span % 2 == 1;
                IEnumerable<SliderEvent> ticks = GenerateTicks(span, spanStartTime, spanDuration, reversed, length, tickDistance, minDistanceFromEnd);

                if (reversed)
                    ticks = ticks.Reverse();

                foreach (SliderEvent e in ticks)
                    yield return e;

                if (span < spanCount - 1)
                    yield return new SliderEvent(NestedSliderObjectType.Repeat, spanStartTime + spanDuration, span, (span + 1) % 2, spanStartTime);
            }

            double totalDuration = spanCount * spanDuration;
            int finalSpanIndex = spanCount - 1;
            double finalSpanStartTime = startTime + finalSpanIndex * spanDuration;
            double legacyLastTickTime = Math.Max(startTime + totalDuration / 2, finalSpanStartTime + spanDuration - 36);
            double legacyLastTickProgress = (legacyLastTickTime - finalSpanStartTime) / spanDuration;

            if (spanCount % 2 == 0)
                legacyLastTickProgress = 1 - legacyLastTickProgress;

            yield return new SliderEvent(NestedSliderObjectType.LegacyLastTick, legacyLastTickTime, finalSpanIndex, legacyLastTickProgress, finalSpanStartTime);
            yield return new SliderEvent(NestedSliderObjectType.Tail, startTime + totalDuration, finalSpanIndex, spanCount % 2, finalSpanStartTime);
        }

        private static IEnumerable<SliderEvent> GenerateTicks(int spanIndex, double spanStartTime, double spanDuration, bool reversed, double length, double tickDistance, double minDistanceFromEnd)
        {
            if (tickDistance == 0)
                yield break;

            for (double d = tickDistance; d <= length; d += tickDistance)
            {
                if (d >= length - minDistanceFromEnd)
                    break;

                double pathProgress = d / length;
                double timeProgress = reversed ? 1 - pathProgress : pathProgress;
                yield return new SliderEvent(NestedSliderObjectType.Tick, spanStartTime + timeProgress * spanDuration, spanIndex, pathProgress, spanStartTime);
            }
        }

        public sealed record SliderEvent(NestedSliderObjectType Type, double Time, int SpanIndex, double PathProgress, double SpanStartTime);
    }

    private sealed class SliderPath
    {
        private readonly List<PathControlPoint> controlPoints;
        private readonly double? expectedDistance;
        private readonly List<Vector2> calculatedPath = new();
        private readonly List<double> cumulativeLength = new();
        private bool valid;
        private double optimisedLength;
        private double calculatedLength;

        public SliderPath(IEnumerable<PathControlPoint> controlPoints, double? expectedDistance)
        {
            this.controlPoints = controlPoints.ToList();
            this.expectedDistance = expectedDistance;
        }

        public bool OptimiseCatmull { get; set; }

        public double Distance
        {
            get
            {
                EnsureValid();
                return cumulativeLength.Count == 0 ? 0 : cumulativeLength[^1];
            }
        }

        public double? ExpectedDistance => expectedDistance;
        public IReadOnlyList<PathControlPoint> ControlPoints => controlPoints;

        public Vector2 PositionAt(double progress)
        {
            EnsureValid();
            double d = Math.Clamp(progress, 0, 1) * Distance;
            return InterpolateVertices(IndexOfDistance(d), d);
        }

        private void EnsureValid()
        {
            if (valid)
                return;

            CalculatePath();
            CalculateLength();
            valid = true;
        }

        private void CalculatePath()
        {
            calculatedPath.Clear();
            optimisedLength = 0;

            if (controlPoints.Count == 0)
                return;

            Vector2[] vertices = controlPoints.Select(c => c.Position).ToArray();
            int start = 0;

            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (controlPoints[i].Type == null && i < controlPoints.Count - 1)
                    continue;

                Vector2[] segmentVertices = vertices.Skip(start).Take(i - start + 1).ToArray();
                PathType segmentType = controlPoints[start].Type ?? PathType.Linear;

                if (segmentVertices.Length == 1)
                {
                    calculatedPath.Add(segmentVertices[0]);
                }
                else
                {
                    List<Vector2> subPath = CalculateSubPath(segmentVertices, segmentType);
                    bool skipFirst = calculatedPath.Count > 0 && subPath.Count > 0 && calculatedPath[^1] == subPath[0];

                    for (int j = skipFirst ? 1 : 0; j < subPath.Count; j++)
                        calculatedPath.Add(subPath[j]);
                }

                start = i;
            }
        }

        private List<Vector2> CalculateSubPath(Vector2[] points, PathType type)
        {
            return type.Kind switch
            {
                PathKind.Linear => points.ToList(),
                PathKind.PerfectCurve when points.Length == 3 && CircularArcProperties.TryCreate(points, out CircularArcProperties arc) => ApproximateCircularArc(arc),
                PathKind.Catmull => CalculateCatmullPath(points),
                _ => ApproximateBezier(points)
            };
        }

        private List<Vector2> CalculateCatmullPath(Vector2[] points)
        {
            List<Vector2> subPath = ApproximateCatmull(points);
            if (!OptimiseCatmull)
                return subPath;

            var optimisedPath = new List<Vector2>(subPath.Count);
            Vector2? lastStart = null;
            double lengthRemovedSinceStart = 0;

            for (int i = 0; i < subPath.Count; i++)
            {
                if (lastStart == null)
                {
                    optimisedPath.Add(subPath[i]);
                    lastStart = subPath[i];
                    continue;
                }

                double distFromStart = Vector2.Distance(lastStart.Value, subPath[i]);
                lengthRemovedSinceStart += Vector2.Distance(subPath[i - 1], subPath[i]);

                if (distFromStart > 6 || (i + 1) % 100 == 0 || i == subPath.Count - 1)
                {
                    optimisedPath.Add(subPath[i]);
                    optimisedLength += lengthRemovedSinceStart - distFromStart;
                    lastStart = null;
                    lengthRemovedSinceStart = 0;
                }
            }

            return optimisedPath;
        }

        private void CalculateLength()
        {
            calculatedLength = optimisedLength;
            cumulativeLength.Clear();
            cumulativeLength.Add(0);

            for (int i = 0; i < calculatedPath.Count - 1; i++)
            {
                calculatedLength += (calculatedPath[i + 1] - calculatedPath[i]).Length;
                cumulativeLength.Add(calculatedLength);
            }

            if (expectedDistance is double distance && calculatedLength != distance)
            {
                if (calculatedPath.Count >= 2 && calculatedPath[^1] == calculatedPath[^2] && distance > calculatedLength)
                {
                    cumulativeLength.Add(calculatedLength);
                    return;
                }

                cumulativeLength.RemoveAt(cumulativeLength.Count - 1);
                int pathEndIndex = calculatedPath.Count - 1;

                if (calculatedLength > distance)
                {
                    while (cumulativeLength.Count > 0 && cumulativeLength[^1] >= distance)
                    {
                        cumulativeLength.RemoveAt(cumulativeLength.Count - 1);
                        calculatedPath.RemoveAt(pathEndIndex--);
                    }
                }

                if (pathEndIndex <= 0)
                {
                    cumulativeLength.Add(0);
                    return;
                }

                Vector2 dir = (calculatedPath[pathEndIndex] - calculatedPath[pathEndIndex - 1]).Normalized();
                calculatedPath[pathEndIndex] = calculatedPath[pathEndIndex - 1] + dir * (float)(distance - cumulativeLength[^1]);
                cumulativeLength.Add(distance);
            }
        }

        private int IndexOfDistance(double d)
        {
            int i = cumulativeLength.BinarySearch(d);
            return i < 0 ? ~i : i;
        }

        private Vector2 InterpolateVertices(int i, double d)
        {
            if (calculatedPath.Count == 0)
                return Vector2.Zero;
            if (i <= 0)
                return calculatedPath[0];
            if (i >= calculatedPath.Count)
                return calculatedPath[^1];

            Vector2 p0 = calculatedPath[i - 1];
            Vector2 p1 = calculatedPath[i];
            double d0 = cumulativeLength[i - 1];
            double d1 = cumulativeLength[i];

            if (AlmostEquals(d0, d1))
                return p0;

            return p0 + (p1 - p0) * (float)((d - d0) / (d1 - d0));
        }
    }

    private sealed class PathControlPoint
    {
        public PathControlPoint(Vector2 position, PathType? type = null)
        {
            Position = position;
            Type = type;
        }

        public Vector2 Position { get; }
        public PathType? Type { get; set; }
    }

    private readonly record struct PathType(PathKind Kind)
    {
        public static readonly PathType Catmull = new(PathKind.Catmull);
        public static readonly PathType Bezier = new(PathKind.Bezier);
        public static readonly PathType Linear = new(PathKind.Linear);
        public static readonly PathType PerfectCurve = new(PathKind.PerfectCurve);
    }

    private enum PathKind
    {
        Catmull,
        Bezier,
        Linear,
        PerfectCurve
    }

    private readonly struct Vector2 : IEquatable<Vector2>
    {
        public static readonly Vector2 Zero = new(0, 0);

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
        public float Length => MathF.Sqrt(X * X + Y * Y);
        public float LengthSquared => X * X + Y * Y;

        public Vector2 Normalized()
        {
            float length = Length;
            return length == 0 ? Zero : new Vector2(X / length, Y / length);
        }

        public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);
        public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);
        public static Vector2 operator *(Vector2 left, float right) => new(left.X * right, left.Y * right);
        public static Vector2 operator *(float left, Vector2 right) => right * left;
        public static Vector2 operator /(Vector2 left, float right) => new(left.X / right, left.Y / right);
        public static Vector2 operator /(Vector2 left, int right) => left / (float)right;
        public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
        public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);
        public static float Dot(Vector2 left, Vector2 right) => left.X * right.X + left.Y * right.Y;
        public static float Distance(Vector2 left, Vector2 right) => (left - right).Length;
    }

    private readonly struct CircularArcProperties
    {
        private CircularArcProperties(Vector2 centre, double thetaStart, double thetaRange, double direction, double radius)
        {
            Centre = centre;
            ThetaStart = thetaStart;
            ThetaRange = thetaRange;
            Direction = direction;
            Radius = radius;
        }

        public Vector2 Centre { get; }
        public double ThetaStart { get; }
        public double ThetaRange { get; }
        public double Direction { get; }
        public double Radius { get; }

        public static bool TryCreate(IReadOnlyList<Vector2> points, out CircularArcProperties properties)
        {
            Vector2 a = points[0];
            Vector2 b = points[1];
            Vector2 c = points[2];
            double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));

            if (Math.Abs(d) < 0.000001)
            {
                properties = default;
                return false;
            }

            double aSq = a.X * a.X + a.Y * a.Y;
            double bSq = b.X * b.X + b.Y * b.Y;
            double cSq = c.X * c.X + c.Y * c.Y;
            var centre = new Vector2(
                (float)((aSq * (b.Y - c.Y) + bSq * (c.Y - a.Y) + cSq * (a.Y - b.Y)) / d),
                (float)((aSq * (c.X - b.X) + bSq * (a.X - c.X) + cSq * (b.X - a.X)) / d));

            Vector2 dA = a - centre;
            Vector2 dC = c - centre;
            double thetaStart = Math.Atan2(dA.Y, dA.X);
            double thetaEnd = Math.Atan2(dC.Y, dC.X);

            while (thetaEnd < thetaStart)
                thetaEnd += 2 * Math.PI;

            double direction = 1;
            double thetaRange = thetaEnd - thetaStart;
            Vector2 orthoAtoC = new Vector2(c.Y - a.Y, -(c.X - a.X));

            if (Vector2.Dot(orthoAtoC, b - a) < 0)
            {
                direction = -direction;
                thetaRange = 2 * Math.PI - thetaRange;
            }

            double radius = Vector2.Distance(a, centre);
            properties = new CircularArcProperties(centre, thetaStart, thetaRange, direction, radius);
            return !double.IsNaN(radius) && radius > 0;
        }
    }

    private static List<Vector2> ApproximateBezier(IReadOnlyList<Vector2> points)
    {
        return BSplineToPiecewiseLinear(points, Math.Max(1, points.Count - 1));
    }

    private static List<Vector2> BSplineToPiecewiseLinear(IReadOnlyList<Vector2> controlPoints, int degree)
    {
        if (degree < 1)
            throw new ArgumentOutOfRangeException(nameof(degree));

        if (controlPoints.Count < 2)
            return controlPoints.Count == 0 ? new List<Vector2>() : new List<Vector2> { controlPoints[0] };

        degree = Math.Min(degree, controlPoints.Count - 1);
        var output = new List<Vector2>();
        Stack<Vector2[]> toFlatten = BSplineToBezierInternal(controlPoints, ref degree);
        var freeBuffers = new Stack<Vector2[]>();
        var subdivisionBuffer1 = new Vector2[degree + 1];
        var subdivisionBuffer2 = new Vector2[degree * 2 + 1];
        Vector2[] leftChild = subdivisionBuffer2;

        while (toFlatten.Count > 0)
        {
            Vector2[] parent = toFlatten.Pop();

            if (BezierIsFlatEnough(parent))
            {
                BezierApproximate(parent, output, subdivisionBuffer1, subdivisionBuffer2, degree + 1);
                freeBuffers.Push(parent);
                continue;
            }

            Vector2[] rightChild = freeBuffers.Count > 0 ? freeBuffers.Pop() : new Vector2[degree + 1];
            BezierSubdivide(parent, leftChild, rightChild, subdivisionBuffer1, degree + 1);

            for (int i = 0; i < degree + 1; ++i)
                parent[i] = leftChild[i];

            toFlatten.Push(rightChild);
            toFlatten.Push(parent);
        }

        output.Add(controlPoints[^1]);
        return output;
    }

    private static Stack<Vector2[]> BSplineToBezierInternal(IReadOnlyList<Vector2> controlPoints, ref int degree)
    {
        var result = new Stack<Vector2[]>();
        degree = Math.Min(degree, controlPoints.Count - 1);
        int pointCount = controlPoints.Count - 1;
        Vector2[] points = controlPoints.ToArray();

        if (degree == pointCount)
        {
            result.Push(points);
        }
        else
        {
            for (int i = 0; i < pointCount - degree; i++)
            {
                var subBezier = new Vector2[degree + 1];
                subBezier[0] = points[i];

                for (int j = 0; j < degree - 1; j++)
                {
                    subBezier[j + 1] = points[i + 1];

                    for (int k = 1; k < degree - j; k++)
                    {
                        int l = Math.Min(k, pointCount - degree - i);
                        points[i + k] = (l * points[i + k] + points[i + k + 1]) / (l + 1);
                    }
                }

                subBezier[degree] = points[i + 1];
                result.Push(subBezier);
            }

            result.Push(points[(pointCount - degree)..]);
            result = new Stack<Vector2[]>(result);
        }

        return result;
    }

    private static bool BezierIsFlatEnough(Vector2[] controlPoints)
    {
        const float tolerance = 0.25f;

        for (int i = 1; i < controlPoints.Length - 1; i++)
        {
            if ((controlPoints[i - 1] - 2 * controlPoints[i] + controlPoints[i + 1]).LengthSquared > tolerance * tolerance * 4)
                return false;
        }

        return true;
    }

    private static void BezierSubdivide(Vector2[] controlPoints, Vector2[] left, Vector2[] right, Vector2[] subdivisionBuffer, int count)
    {
        for (int i = 0; i < count; ++i)
            subdivisionBuffer[i] = controlPoints[i];

        for (int i = 0; i < count; i++)
        {
            left[i] = subdivisionBuffer[0];
            right[count - i - 1] = subdivisionBuffer[count - i - 1];

            for (int j = 0; j < count - i - 1; j++)
                subdivisionBuffer[j] = (subdivisionBuffer[j] + subdivisionBuffer[j + 1]) / 2;
        }
    }

    private static void BezierApproximate(Vector2[] controlPoints, List<Vector2> output, Vector2[] subdivisionBuffer1, Vector2[] subdivisionBuffer2, int count)
    {
        Vector2[] left = subdivisionBuffer2;
        Vector2[] right = subdivisionBuffer1;
        BezierSubdivide(controlPoints, left, right, subdivisionBuffer1, count);

        for (int i = 0; i < count - 1; ++i)
            left[count + i] = right[i + 1];

        output.Add(controlPoints[0]);

        for (int i = 1; i < count - 1; ++i)
        {
            int index = 2 * i;
            Vector2 p = 0.25f * (left[index - 1] + 2 * left[index] + left[index + 1]);
            output.Add(p);
        }
    }

    private static List<Vector2> ApproximateCatmull(IReadOnlyList<Vector2> points)
    {
        const int detail = 50;
        var result = new List<Vector2>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 v1 = i > 0 ? points[i - 1] : points[i];
            Vector2 v2 = points[i];
            Vector2 v3 = i < points.Count - 1 ? points[i + 1] : v2 + v2 - v1;
            Vector2 v4 = i < points.Count - 2 ? points[i + 2] : v3 + v3 - v2;

            for (int c = 0; c < detail; c++)
            {
                float t = (float)c / detail;
                Vector2 point = new Vector2(
                    0.5f * (2 * v2.X + (-v1.X + v3.X) * t + (2 * v1.X - 5 * v2.X + 4 * v3.X - v4.X) * t * t + (-v1.X + 3 * v2.X - 3 * v3.X + v4.X) * t * t * t),
                    0.5f * (2 * v2.Y + (-v1.Y + v3.Y) * t + (2 * v1.Y - 5 * v2.Y + 4 * v3.Y - v4.Y) * t * t + (-v1.Y + 3 * v2.Y - 3 * v3.Y + v4.Y) * t * t * t));
                result.Add(point);
                t = (float)(c + 1) / detail;
                result.Add(new Vector2(
                    0.5f * (2 * v2.X + (-v1.X + v3.X) * t + (2 * v1.X - 5 * v2.X + 4 * v3.X - v4.X) * t * t + (-v1.X + 3 * v2.X - 3 * v3.X + v4.X) * t * t * t),
                    0.5f * (2 * v2.Y + (-v1.Y + v3.Y) * t + (2 * v1.Y - 5 * v2.Y + 4 * v3.Y - v4.Y) * t * t + (-v1.Y + 3 * v2.Y - 3 * v3.Y + v4.Y) * t * t * t)));
            }
        }
        return result;
    }

    private static List<Vector2> ApproximateCircularArc(CircularArcProperties arc)
    {
        int amountPoints = 2 * arc.Radius <= 0.1 ? 2 : Math.Max(2, (int)Math.Ceiling(arc.ThetaRange / (2.0 * Math.Acos(1f - 0.1f / arc.Radius))));
        var output = new List<Vector2>(amountPoints);

        for (int i = 0; i < amountPoints; i++)
        {
            double fract = (double)i / (amountPoints - 1);
            double theta = arc.ThetaStart + arc.Direction * fract * arc.ThetaRange;
            output.Add(new Vector2((float)(Math.Cos(theta) * arc.Radius + arc.Centre.X), (float)(Math.Sin(theta) * arc.Radius + arc.Centre.Y)));
        }

        return output;
    }

    private static void ApplyStacking(Beatmap beatmap)
    {
        List<OsuHitObject> hitObjects = beatmap.HitObjects;

        foreach (OsuHitObject h in hitObjects)
            h.StackHeight = 0;

        if (hitObjects.Count == 0)
            return;

        if (beatmap.FormatVersion >= 6)
            ApplyStackingNew(beatmap, hitObjects, 0, hitObjects.Count - 1);
        else
            ApplyStackingOld(beatmap, hitObjects);

        foreach (OsuHitObject obj in hitObjects.OfType<Slider>())
            ((Slider)obj).BuildNestedObjects();
    }

    private static void ApplyStackingNew(Beatmap beatmap, List<OsuHitObject> hitObjects, int startIndex, int endIndex)
    {
        int extendedEndIndex = endIndex;

        if (endIndex < hitObjects.Count - 1)
        {
            for (int i = endIndex; i >= startIndex; i--)
            {
                int stackBaseIndex = i;

                for (int n = stackBaseIndex + 1; n < hitObjects.Count; n++)
                {
                    OsuHitObject stackBaseObject = hitObjects[stackBaseIndex];
                    if (stackBaseObject is Spinner)
                        break;

                    OsuHitObject objectN = hitObjects[n];
                    if (objectN is Spinner)
                        continue;

                    double endTime = stackBaseObject.EndTime;
                    float stackThreshold = CalculateStackThreshold(beatmap, objectN);

                    if (objectN.StartTime - endTime > stackThreshold)
                        break;

                    if (Vector2.Distance(stackBaseObject.Position, objectN.Position) < 3 || stackBaseObject is Slider && Vector2.Distance(stackBaseObject.EndPosition, objectN.Position) < 3)
                    {
                        stackBaseIndex = n;
                        objectN.StackHeight = 0;
                    }
                }

                if (stackBaseIndex > extendedEndIndex)
                {
                    extendedEndIndex = stackBaseIndex;
                    if (extendedEndIndex == hitObjects.Count - 1)
                        break;
                }
            }
        }

        int extendedStartIndex = startIndex;

        for (int i = extendedEndIndex; i > startIndex; i--)
        {
            int n = i;
            OsuHitObject objectI = hitObjects[i];
            if (objectI.StackHeight != 0 || objectI is Spinner)
                continue;

            float stackThreshold = CalculateStackThreshold(beatmap, objectI);

            if (objectI is HitCircle)
            {
                while (--n >= 0)
                {
                    OsuHitObject objectN = hitObjects[n];
                    if (objectN is Spinner)
                        continue;

                    double endTime = objectN.EndTime;
                    if ((int)objectI.StartTime - (int)endTime > stackThreshold)
                        break;

                    if (n < extendedStartIndex)
                    {
                        objectN.StackHeight = 0;
                        extendedStartIndex = n;
                    }

                    if (objectN is Slider && Vector2.Distance(objectN.EndPosition, objectI.Position) < 3)
                    {
                        int offset = objectI.StackHeight - objectN.StackHeight + 1;

                        for (int j = n + 1; j <= i; j++)
                        {
                            OsuHitObject objectJ = hitObjects[j];
                            if (Vector2.Distance(objectN.EndPosition, objectJ.Position) < 3)
                                objectJ.StackHeight -= offset;
                        }

                        break;
                    }

                    if (Vector2.Distance(objectN.Position, objectI.Position) < 3)
                    {
                        objectN.StackHeight = objectI.StackHeight + 1;
                        objectI = objectN;
                    }
                }
            }
            else if (objectI is Slider)
            {
                while (--n >= startIndex)
                {
                    OsuHitObject objectN = hitObjects[n];
                    if (objectN is Spinner)
                        continue;

                    if (objectI.StartTime - objectN.StartTime > stackThreshold)
                        break;

                    if (Vector2.Distance(objectN.EndPosition, objectI.Position) < 3)
                    {
                        objectN.StackHeight = objectI.StackHeight + 1;
                        objectI = objectN;
                    }
                }
            }
        }
    }

    private static void ApplyStackingOld(Beatmap beatmap, List<OsuHitObject> hitObjects)
    {
        for (int i = 0; i < hitObjects.Count; i++)
        {
            OsuHitObject currHitObject = hitObjects[i];
            if (currHitObject.StackHeight != 0 && currHitObject is not Slider)
                continue;

            double startTime = currHitObject.EndTime;
            int sliderStack = 0;

            for (int j = i + 1; j < hitObjects.Count; j++)
            {
                float stackThreshold = CalculateStackThreshold(beatmap, hitObjects[i]);
                if (hitObjects[j].StartTime - stackThreshold > startTime)
                    break;

                Vector2 position2 = currHitObject is Slider slider ? slider.Position + slider.Path.PositionAt(1) : currHitObject.Position;

                if (Vector2.Distance(hitObjects[j].Position, currHitObject.Position) < 3)
                {
                    currHitObject.StackHeight++;
                    startTime = hitObjects[j].StartTime;
                }
                else if (Vector2.Distance(hitObjects[j].Position, position2) < 3)
                {
                    sliderStack++;
                    hitObjects[j].StackHeight -= sliderStack;
                    startTime = hitObjects[j].StartTime;
                }
            }
        }
    }

    private static float CalculateStackThreshold(Beatmap beatmap, OsuHitObject hitObject) => (int)hitObject.TimePreempt * beatmap.StackLeniency;

    private static double CalculateDifficultyRating(double difficultyValue) => Math.Sqrt(difficultyValue) * 0.0675;

    private static double DifficultyRange(double difficulty, double min, double mid, double max)
    {
        if (difficulty > 5)
            return mid + (max - mid) * (difficulty - 5) / 5;
        if (difficulty < 5)
            return mid + (mid - min) * (difficulty - 5) / 5;

        return mid;
    }

    private static double InverseDifficultyRange(double value, double min, double mid, double max)
    {
        if (value > mid)
            return 5 + (value - mid) / (mid - min) * 5;

        if (value < mid)
            return 5 + (value - mid) / (max - mid) * 5;

        return 5;
    }

    private static int DifficultyRangeInt(double difficulty, double min, double mid, double max) => (int)DifficultyRange(difficulty, min, mid, max);

    private static double GreatHitWindow(double overallDifficulty) => Math.Floor(DifficultyRange(overallDifficulty, 80, 50, 20)) - 0.5;

    private static double CalculateRateAdjustedApproachRate(double approachRate, double clockRate)
    {
        double preempt = DifficultyRange(approachRate, 1800, 1200, 450) / clockRate;
        return InverseDifficultyRange(preempt, 1800, 1200, 450);
    }

    private static double CalculateRateAdjustedOverallDifficulty(double overallDifficulty, double clockRate)
    {
        double hitWindowGreat = GreatHitWindow(overallDifficulty) / clockRate;
        return (79.5 - hitWindowGreat) / 6;
    }

    private static double PrecisionAdjustedBeatLength(double sliderVelocityMultiplier, double beatLength, double maxSliderVelocity)
    {
        double sliderVelocityAsBeatLength = -100 / sliderVelocityMultiplier;
        double bpmMultiplier = sliderVelocityAsBeatLength < 0 ? Math.Clamp((float)-sliderVelocityAsBeatLength, 10, maxSliderVelocity) / 100.0 : 1;
        return beatLength * bpmMultiplier;
    }

    private static float CalculateScaleFromCircleSize(float circleSize, bool applyFudge = true) => (float)((1.0f - 0.7f * ((circleSize - 5) / 5)) / 2 * (applyFudge ? 1.00041f : 1));

    private static double BpmToMilliseconds(double bpm, int delimiter = 4) => 60000.0 / delimiter / bpm;

    private static double MillisecondsToBpm(double ms, int delimiter = 4) => 60000.0 / (ms * delimiter);

    private static double Smoothstep(double x, double start, double end)
    {
        x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);
        return x * x * (3.0 - 2.0 * x);
    }

    private static double Smootherstep(double x, double start, double end)
    {
        x = Math.Clamp((x - start) / (end - start), 0.0, 1.0);
        return x * x * x * (x * (6.0 * x - 15.0) + 10.0);
    }

    private static double SmoothstepBellCurve(double x, double mean = 0.5, double width = 0.5)
    {
        x -= mean;
        x = x > 0 ? width - x : width + x;
        return Smoothstep(x, 0, width);
    }

    private static double ReverseLerp(double x, double start, double end) => Math.Clamp((x - start) / (end - start), 0.0, 1.0);

    private static double Logistic(double x, double midpointOffset, double multiplier, double maxValue = 1) => maxValue / (1 + Math.Exp(multiplier * (midpointOffset - x)));

    private static double Lerp(double start, double end, double amount) => start + (end - start) * amount;

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double Cbrt(double x) => x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);

    private static bool AlmostEquals(double a, double b, double tolerance = 0.000001) => Math.Abs(a - b) <= tolerance;

    private static int ParseInt(string value) => int.Parse(value.Trim(), CultureInfo.InvariantCulture);

    private static float ParseFloat(string value) => float.Parse(value.Trim(), CultureInfo.InvariantCulture);

    private static double ParseDouble(string value) => double.Parse(value.Trim(), CultureInfo.InvariantCulture);
}
