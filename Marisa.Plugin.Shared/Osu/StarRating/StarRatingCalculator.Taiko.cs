namespace Marisa.Plugin.Shared.Osu;

public static partial class StarRatingCalculator
{
    private sealed class TaikoHit : OsuHitObject
    {
        public TaikoHit(Vector2 position, double startTime, bool rim)
            : base(position, startTime)
        {
            Rim = rim;
        }

        public bool Rim { get; }
    }

    private sealed class TaikoDrumRoll : OsuHitObject
    {
        public TaikoDrumRoll(Vector2 position, double startTime, double endTime)
            : base(position, startTime)
        {
            EndTimeValue = endTime;
        }

        public TaikoDrumRoll(Vector2 position, double startTime, SliderPath path, int repeatCount)
            : base(position, startTime)
        {
            Path = path;
            RepeatCount = repeatCount;
        }

        public SliderPath? Path { get; }
        public int RepeatCount { get; }
        public double EndTimeValue { get; set; }
        public override double EndTime => EndTimeValue;
    }

    private sealed class TaikoDifficultyCalculator
    {
        private readonly Beatmap beatmap;
        private readonly double clockRate;

        public TaikoDifficultyCalculator(Beatmap beatmap, double clockRate)
        {
            this.beatmap = beatmap;
            this.clockRate = clockRate;
        }

        public double CalculateStarRating()
        {
            List<TaikoDifficultyHitObject> difficultyObjects = CreateDifficultyObjects();
            if (difficultyObjects.Count == 0)
                return 0;

            TaikoColourPreprocessor.ProcessAndAssign(difficultyObjects);
            TaikoRhythmPreprocessor.ProcessAndAssign(difficultyObjects.Where(o => o.BaseObject is TaikoHit).ToList());

            var rhythm = new TaikoRhythmSkill(TaikoGreatHitWindow(beatmap.Difficulty.OverallDifficulty) / clockRate);
            var reading = new TaikoReadingSkill();
            var colour = new TaikoColourSkill();
            var stamina = new TaikoStaminaSkill(singleColourStamina: false, isConvert: false);

            foreach (TaikoDifficultyHitObject difficultyObject in difficultyObjects)
            {
                rhythm.Process(difficultyObject);
                reading.Process(difficultyObject);
                colour.Process(difficultyObject);
                stamina.Process(difficultyObject);
            }

            double rhythmSkill = rhythm.DifficultyValue() * 0.750 * 0.084375;
            double colourSkill = colour.DifficultyValue() * 0.375 * 0.084375;
            double staminaSkill = stamina.DifficultyValue() * 0.445 * 0.084375;
            double staminaDifficultStrains = stamina.CountTopWeightedStrains();
            double patternMultiplier = Math.Pow(staminaSkill * colourSkill, 0.10);
            double strainLengthBonus = 1 + 0.15 * ReverseLerp(staminaDifficultStrains, 1000, 1555);
            List<double> peaks = CombineTaikoPeaks(rhythm.CurrentStrainPeaksPublic().ToList(), reading.CurrentStrainPeaksPublic().ToList(), colour.CurrentStrainPeaksPublic().ToList(), stamina.CurrentStrainPeaksPublic().ToList(), patternMultiplier, strainLengthBonus);
            double difficulty = 0;
            double weight = 1;

            foreach (double strain in peaks.OrderByDescending(p => p))
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            return difficulty < 0 ? difficulty : 10.43 * Math.Log(difficulty * 1.4 / 8 + 1);
        }

        private List<TaikoDifficultyHitObject> CreateDifficultyObjects()
        {
            var result = new List<TaikoDifficultyHitObject>();
            var centre = new List<TaikoDifficultyHitObject>();
            var rim = new List<TaikoDifficultyHitObject>();
            var notes = new List<TaikoDifficultyHitObject>();

            for (int i = 2; i < beatmap.HitObjects.Count; i++)
            {
                var current = new TaikoDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], result, centre, rim, notes, result.Count, beatmap, clockRate);
                result.Add(current);
            }

            return result;
        }

        private static List<double> CombineTaikoPeaks(List<double> rhythmPeaks, List<double> readingPeaks, List<double> colourPeaks, List<double> staminaPeaks, double patternMultiplier, double strainLengthBonus)
        {
            var combined = new List<double>();

            for (int i = 0; i < colourPeaks.Count; i++)
            {
                double rhythmPeak = rhythmPeaks[i] * 0.750 * 0.084375 * patternMultiplier;
                double readingPeak = readingPeaks[i] * 0.100 * 0.084375;
                double colourPeak = colourPeaks[i] * 0.375 * 0.084375;
                double staminaPeak = staminaPeaks[i] * 0.445 * 0.084375 * strainLengthBonus;
                double peak = NormValue(2, NormValue(1.5, colourPeak, staminaPeak), rhythmPeak, readingPeak);

                if (peak > 0)
                    combined.Add(peak);
            }

            return combined;
        }
    }

    private sealed class TaikoDifficultyHitObject : SimpleDifficultyHitObject, IHasInterval
    {
        private readonly IReadOnlyList<TaikoDifficultyHitObject>? monoObjects;
        private readonly IReadOnlyList<TaikoDifficultyHitObject> noteObjects;

        public TaikoDifficultyHitObject(OsuHitObject hitObject, OsuHitObject lastObject, List<TaikoDifficultyHitObject> objects, List<TaikoDifficultyHitObject> centreObjects, List<TaikoDifficultyHitObject> rimObjects, List<TaikoDifficultyHitObject> noteObjects, int index, Beatmap beatmap, double clockRate)
            : base(hitObject, lastObject, objects.Cast<SimpleDifficultyHitObject>().ToList(), index, clockRate)
        {
            this.noteObjects = noteObjects;
            ColourData = new TaikoColourData();

            if (hitObject is TaikoHit hit)
            {
                if (hit.Rim)
                {
                    MonoIndex = rimObjects.Count;
                    rimObjects.Add(this);
                    monoObjects = rimObjects;
                }
                else
                {
                    MonoIndex = centreObjects.Count;
                    centreObjects.Add(this);
                    monoObjects = centreObjects;
                }

                NoteIndex = noteObjects.Count;
                noteObjects.Add(this);
            }

            RhythmData = new TaikoRhythmData(this);
            TimingPoint timingPoint = beatmap.TimingPointAt(hitObject.StartTime);
            double bpm = 60000.0 / timingPoint.BeatLength;
            EffectiveBPM = bpm * beatmap.Difficulty.SliderMultiplier * beatmap.ScrollSpeedAt(hitObject.StartTime) * clockRate;
        }

        public int MonoIndex { get; }
        public int NoteIndex { get; }
        public TaikoRhythmData RhythmData { get; }
        public TaikoColourData ColourData { get; }
        public double EffectiveBPM { get; }
        public double Interval => DeltaTime;

        public TaikoDifficultyHitObject? PreviousMono(int backwardsIndex) => monoObjects?.ElementAtOrDefault(MonoIndex - (backwardsIndex + 1));
        public TaikoDifficultyHitObject? PreviousNote(int backwardsIndex) => noteObjects.ElementAtOrDefault(NoteIndex - (backwardsIndex + 1));
        public TaikoDifficultyHitObject? NextNote(int forwardsIndex) => noteObjects.ElementAtOrDefault(NoteIndex + (forwardsIndex + 1));
    }

    private sealed class TaikoRhythmSkill : SimpleStrainDecaySkill
    {
        private readonly double greatHitWindow;

        public TaikoRhythmSkill(double greatHitWindow)
            : base(1, 0.4)
        {
            this.greatHitWindow = greatHitWindow;
        }

        protected override double StrainValueOf(SimpleDifficultyHitObject current)
        {
            double difficulty = TaikoEvaluators.Rhythm((TaikoDifficultyHitObject)current, greatHitWindow);
            double staminaDifficulty = TaikoEvaluators.Stamina((TaikoDifficultyHitObject)current) - 0.5;
            return difficulty * Logistic(staminaDifficulty, 1 / 15.0, 50.0);
        }
    }

    private sealed class TaikoReadingSkill : SimpleStrainDecaySkill
    {
        private double currentStrain;

        public TaikoReadingSkill()
            : base(1, 0.4)
        {
        }

        protected override double StrainValueOf(SimpleDifficultyHitObject current)
        {
            if (current.BaseObject is not TaikoHit)
                return 0;

            var taikoObject = (TaikoDifficultyHitObject)current;
            int index = taikoObject.ColourData.MonoStreak?.HitObjects.IndexOf(taikoObject) ?? 0;
            currentStrain *= Logistic(index, 4, -1 / 25.0, 0.5) + 0.5;
            currentStrain *= 0.4;
            currentStrain += TaikoEvaluators.Reading(taikoObject);
            return currentStrain;
        }
    }

    private sealed class TaikoColourSkill : SimpleStrainDecaySkill
    {
        public TaikoColourSkill()
            : base(0.12, 0.8)
        {
        }

        protected override double StrainValueOf(SimpleDifficultyHitObject current) => TaikoEvaluators.Colour((TaikoDifficultyHitObject)current);
    }

    private sealed class TaikoStaminaSkill : SimpleStrainSkill
    {
        private readonly bool singleColourStamina;
        private readonly bool isConvert;
        private double currentStrain;

        public TaikoStaminaSkill(bool singleColourStamina, bool isConvert)
        {
            this.singleColourStamina = singleColourStamina;
            this.isConvert = isConvert;
        }

        protected override double StrainValueAt(SimpleDifficultyHitObject current)
        {
            currentStrain *= Math.Pow(0.4, current.DeltaTime / 1000);
            double staminaDifficulty = TaikoEvaluators.Stamina((TaikoDifficultyHitObject)current) * 1.1;
            var currentObject = (TaikoDifficultyHitObject)current;
            int index = currentObject.ColourData.MonoStreak?.HitObjects.IndexOf(currentObject) ?? 0;
            double monoLengthBonus = isConvert ? 1.0 : 1.0 + 0.5 * ReverseLerp(index, 5, 20);

            if (!singleColourStamina)
                staminaDifficulty *= monoLengthBonus;

            currentStrain += staminaDifficulty;
            return singleColourStamina ? LogisticExponent(-(index - 10) / 2.0, currentStrain) : currentStrain;
        }

        protected override double CalculateInitialStrain(double time, SimpleDifficultyHitObject current)
            => singleColourStamina ? 0 : currentStrain * Math.Pow(0.4, (time - current.Previous(0)!.StartTime) / 1000);
    }

    private static class TaikoEvaluators
    {
        public static double Stamina(TaikoDifficultyHitObject current)
        {
            if (current.BaseObject is not TaikoHit)
                return 0;

            var previous = current.Previous(1) as TaikoDifficultyHitObject;
            TaikoDifficultyHitObject? previousMono = current.PreviousMono(AvailableFingersFor(current) - 1);
            double objectStrain = 0.5;
            if (previous == null) return objectStrain;

            if (previousMono != null)
                objectStrain += SpeedBonus(current.StartTime - previousMono.StartTime) + 0.5 * SpeedBonus(current.StartTime - previous.StartTime);

            return objectStrain;
        }

        public static double Reading(TaikoDifficultyHitObject noteObject)
        {
            double effectiveBPM = Math.Max(1.0, noteObject.EffectiveBPM);
            double midVelocityDifficulty = 0.5 * Logistic(effectiveBPM, 420, 1.0 / 12);
            double expectedDeltaTime = 21000.0 / effectiveBPM;
            double objectDensity = expectedDeltaTime / Math.Max(1.0, noteObject.DeltaTime);
            double densityPenalty = Logistic(objectDensity, 0.925, 15);
            double highVelocityDifficulty = (1.0 - 0.33 * densityPenalty) * Logistic(effectiveBPM, 560 + 8 * densityPenalty, (1.0 + 0.5 * densityPenalty) / 16);
            return midVelocityDifficulty + highVelocityDifficulty;
        }

        public static double Colour(TaikoDifficultyHitObject hitObject)
        {
            TaikoColourData data = hitObject.ColourData;
            double difficulty = 0;

            if (data.MonoStreak?.FirstHitObject == hitObject)
                difficulty += LogisticExponent(Math.E * data.MonoStreak.Index - 2 * Math.E) * EvaluateAlternating(data.MonoStreak.Parent) * 0.5;

            if (data.AlternatingMonoPattern?.FirstHitObject == hitObject)
                difficulty += EvaluateAlternating(data.AlternatingMonoPattern);

            if (data.RepeatingHitPattern?.FirstHitObject == hitObject)
                difficulty += EvaluateRepeating(data.RepeatingHitPattern);

            return difficulty * ConsistentRatioPenalty(hitObject);
        }

        public static double Rhythm(TaikoDifficultyHitObject hitObject, double hitWindow)
        {
            TaikoRhythmData rhythmData = hitObject.RhythmData;
            double sameRhythm = 0;
            double samePattern = 0;
            double intervalPenalty = 0;

            if (rhythmData.SameRhythmGroupedHitObjects?.FirstHitObject == hitObject)
            {
                sameRhythm += 10.0 * EvaluateSameRhythm(rhythmData.SameRhythmGroupedHitObjects, hitWindow);
                intervalPenalty = RepeatedIntervalPenalty(rhythmData.SameRhythmGroupedHitObjects, hitWindow);
            }

            if (rhythmData.SamePatternsGroupedHitObjects?.FirstHitObject == hitObject)
                samePattern += 1.15 * RatioDifficulty(rhythmData.SamePatternsGroupedHitObjects.IntervalRatio);

            return Math.Max(sameRhythm, samePattern) * intervalPenalty;
        }

        private static int AvailableFingersFor(TaikoDifficultyHitObject hitObject)
        {
            TaikoDifficultyHitObject? previousColourChange = hitObject.ColourData.PreviousColourChange;
            TaikoDifficultyHitObject? nextColourChange = hitObject.ColourData.NextColourChange;
            if (previousColourChange != null && hitObject.StartTime - previousColourChange.StartTime < 300) return 2;
            if (nextColourChange != null && nextColourChange.StartTime - hitObject.StartTime < 300) return 2;
            return 8;
        }

        private static double SpeedBonus(double interval) => 20 / Math.Max(interval, 1);
        private static double EvaluateAlternating(TaikoAlternatingMonoPattern p) => LogisticExponent(Math.E * p.Index - 2 * Math.E) * EvaluateRepeating(p.Parent);
        private static double EvaluateRepeating(TaikoRepeatingHitPatterns p) => 2 * (1 - LogisticExponent(Math.E * p.RepetitionInterval - 2 * Math.E));

        private static double ConsistentRatioPenalty(TaikoDifficultyHitObject hitObject)
        {
            int consistentRatioCount = 0;
            double totalRatioCount = 0.0;
            var recentRatios = new List<double>();
            TaikoDifficultyHitObject current = hitObject;
            var previousHitObject = (TaikoDifficultyHitObject?)current.Previous(1);

            for (int i = 0; i < 64; i++)
            {
                if (current.Index <= 1 || previousHitObject == null)
                    break;

                double currentRatio = current.RhythmData.Ratio;
                double previousRatio = previousHitObject.RhythmData.Ratio;
                recentRatios.Add(currentRatio);

                if (Math.Abs(1 - currentRatio / previousRatio) <= 0.01)
                {
                    consistentRatioCount++;
                    totalRatioCount += currentRatio;
                    break;
                }

                current = previousHitObject;
            }

            if (consistentRatioCount > 0)
                return 1 - totalRatioCount / (consistentRatioCount + 1) * 0.80;

            if (recentRatios.Count <= 1) return 1.0;
            double maxRatioDeviation = recentRatios.Max(r => Math.Abs(r - recentRatios.Average()));
            return 0.7 + 0.3 * Smootherstep(maxRatioDeviation, 0.0, 1.0);
        }

        private static double EvaluateSameRhythm(TaikoSameRhythmGroup group, double hitWindow)
        {
            double intervalDifficulty = RatioDifficulty(group.HitObjectIntervalRatio);
            intervalDifficulty *= RepeatedIntervalPenalty(group, hitWindow);

            if (group.Previous?.HitObjectInterval is double previousInterval && group.HitObjects.Count > 1)
            {
                double expectedDurationFromPrevious = previousInterval * group.HitObjects.Count;
                double durationDifference = group.Duration - expectedDurationFromPrevious;
                if (durationDifference > 0)
                    intervalDifficulty *= Logistic(durationDifference / hitWindow, midpointOffset: 0.7, multiplier: 1.0);
            }

            intervalDifficulty *= Logistic(group.Duration / hitWindow, midpointOffset: 0.6, multiplier: 1);
            return Math.Pow(intervalDifficulty, 0.75);
        }

        private static double RepeatedIntervalPenalty(TaikoSameRhythmGroup group, double hitWindow)
        {
            double longIntervalPenalty = SameInterval(group, 3);
            double shortIntervalPenalty = group.HitObjects.Count < 6 ? SameInterval(group, 4) : 1.0;
            double durationPenalty = Math.Max(1 - group.Duration * 2 / hitWindow, 0.5);
            return Math.Min(longIntervalPenalty, shortIntervalPenalty) * durationPenalty;
        }

        private static double SameInterval(TaikoSameRhythmGroup start, int intervalCount)
        {
            var intervals = new List<double?>();
            TaikoSameRhythmGroup? current = start;
            for (int i = 0; i < intervalCount && current != null; i++)
            {
                intervals.Add(current.HitObjectInterval);
                current = current.Previous;
            }

            intervals.RemoveAll(interval => interval == null);
            if (intervals.Count < intervalCount) return 1.0;

            for (int i = 0; i < intervals.Count; i++)
            for (int j = i + 1; j < intervals.Count; j++)
            {
                double ratio = intervals[i]!.Value / intervals[j]!.Value;
                if (Math.Abs(1 - ratio) <= 0.1) return 0.80;
            }

            return 1.0;
        }

        private static double RatioDifficulty(double ratio, int terms = 8)
        {
            double difficulty = 0;
            ratio = double.IsNormal(ratio) ? ratio : 0;
            for (int i = 1; i <= terms; ++i)
                difficulty += -Math.Pow(Math.Cos(i * Math.PI * ratio), 4);

            difficulty += terms / (1 + ratio);
            difficulty += BellCurveValue(ratio, 1, 0.5);
            difficulty -= BellCurveValue(ratio, 1, 0.3);
            return Math.Max(difficulty, 0) / Math.Sqrt(8);
        }
    }

    private interface IHasInterval
    {
        double Interval { get; }
    }

    private sealed class TaikoColourData
    {
        public TaikoMonoStreak? MonoStreak;
        public TaikoAlternatingMonoPattern? AlternatingMonoPattern;
        public TaikoRepeatingHitPatterns? RepeatingHitPattern;
        public TaikoDifficultyHitObject? PreviousColourChange => MonoStreak?.FirstHitObject.PreviousNote(0);
        public TaikoDifficultyHitObject? NextColourChange => MonoStreak?.LastHitObject.NextNote(0);
    }

    private sealed class TaikoMonoStreak
    {
        public List<TaikoDifficultyHitObject> HitObjects { get; } = new();
        public TaikoAlternatingMonoPattern Parent = null!;
        public int Index;
        public TaikoDifficultyHitObject FirstHitObject => HitObjects[0];
        public TaikoDifficultyHitObject LastHitObject => HitObjects[^1];
        public bool? Rim => (HitObjects[0].BaseObject as TaikoHit)?.Rim;
        public int RunLength => HitObjects.Count;
    }

    private sealed class TaikoAlternatingMonoPattern
    {
        public List<TaikoMonoStreak> MonoStreaks { get; } = new();
        public TaikoRepeatingHitPatterns Parent = null!;
        public int Index;
        public TaikoDifficultyHitObject FirstHitObject => MonoStreaks[0].FirstHitObject;
        public bool IsRepetitionOf(TaikoAlternatingMonoPattern other) => HasIdenticalMonoLength(other) && other.MonoStreaks.Count == MonoStreaks.Count && other.MonoStreaks[0].Rim == MonoStreaks[0].Rim;
        public bool HasIdenticalMonoLength(TaikoAlternatingMonoPattern other) => other.MonoStreaks[0].RunLength == MonoStreaks[0].RunLength;
    }

    private sealed class TaikoRepeatingHitPatterns
    {
        private const int MaxRepetitionInterval = 16;
        public List<TaikoAlternatingMonoPattern> AlternatingMonoPatterns { get; } = new();
        public TaikoDifficultyHitObject FirstHitObject => AlternatingMonoPatterns[0].FirstHitObject;
        public TaikoRepeatingHitPatterns? Previous { get; }
        public int RepetitionInterval { get; private set; } = MaxRepetitionInterval + 1;

        public TaikoRepeatingHitPatterns(TaikoRepeatingHitPatterns? previous)
        {
            Previous = previous;
        }

        public void FindRepetitionInterval()
        {
            TaikoRepeatingHitPatterns? other = Previous;
            int interval = 1;
            while (interval < MaxRepetitionInterval && other != null)
            {
                if (IsRepetitionOf(other))
                {
                    RepetitionInterval = Math.Min(interval, MaxRepetitionInterval);
                    return;
                }

                other = other.Previous;
                interval++;
            }

            RepetitionInterval = MaxRepetitionInterval + 1;
        }

        private bool IsRepetitionOf(TaikoRepeatingHitPatterns other)
        {
            if (AlternatingMonoPatterns.Count != other.AlternatingMonoPatterns.Count) return false;
            for (int i = 0; i < Math.Min(AlternatingMonoPatterns.Count, 2); i++)
                if (!AlternatingMonoPatterns[i].HasIdenticalMonoLength(other.AlternatingMonoPatterns[i])) return false;
            return true;
        }
    }

    private static class TaikoColourPreprocessor
    {
        public static void ProcessAndAssign(List<TaikoDifficultyHitObject> hitObjects)
        {
            List<TaikoMonoStreak> monoStreaks = EncodeMonoStreak(hitObjects.Cast<SimpleDifficultyHitObject>().ToList());
            List<TaikoAlternatingMonoPattern> monoPatterns = EncodeAlternatingMonoPattern(monoStreaks);
            List<TaikoRepeatingHitPatterns> hitPatterns = EncodeRepeatingHitPattern(monoPatterns);

            foreach (TaikoRepeatingHitPatterns repeating in hitPatterns)
            {
                for (int i = 0; i < repeating.AlternatingMonoPatterns.Count; i++)
                {
                    TaikoAlternatingMonoPattern monoPattern = repeating.AlternatingMonoPatterns[i];
                    monoPattern.Parent = repeating;
                    monoPattern.Index = i;

                    for (int j = 0; j < monoPattern.MonoStreaks.Count; j++)
                    {
                        TaikoMonoStreak monoStreak = monoPattern.MonoStreaks[j];
                        monoStreak.Parent = monoPattern;
                        monoStreak.Index = j;

                        foreach (TaikoDifficultyHitObject hitObject in monoStreak.HitObjects)
                        {
                            hitObject.ColourData.RepeatingHitPattern = repeating;
                            hitObject.ColourData.AlternatingMonoPattern = monoPattern;
                            hitObject.ColourData.MonoStreak = monoStreak;
                        }
                    }
                }
            }
        }

        private static List<TaikoMonoStreak> EncodeMonoStreak(List<SimpleDifficultyHitObject> data)
        {
            var monoStreaks = new List<TaikoMonoStreak>();
            TaikoMonoStreak? current = null;

            foreach (TaikoDifficultyHitObject taikoObject in data.Cast<TaikoDifficultyHitObject>())
            {
                TaikoDifficultyHitObject? previousObject = taikoObject.PreviousNote(0);
                if (current == null || previousObject == null || (taikoObject.BaseObject as TaikoHit)?.Rim != (previousObject.BaseObject as TaikoHit)?.Rim)
                {
                    current = new TaikoMonoStreak();
                    monoStreaks.Add(current);
                }

                current.HitObjects.Add(taikoObject);
            }

            return monoStreaks;
        }

        private static List<TaikoAlternatingMonoPattern> EncodeAlternatingMonoPattern(List<TaikoMonoStreak> data)
        {
            var monoPatterns = new List<TaikoAlternatingMonoPattern>();
            TaikoAlternatingMonoPattern? current = null;

            for (int i = 0; i < data.Count; i++)
            {
                if (current == null || data[i].RunLength != data[i - 1].RunLength)
                {
                    current = new TaikoAlternatingMonoPattern();
                    monoPatterns.Add(current);
                }

                current.MonoStreaks.Add(data[i]);
            }

            return monoPatterns;
        }

        private static List<TaikoRepeatingHitPatterns> EncodeRepeatingHitPattern(List<TaikoAlternatingMonoPattern> data)
        {
            var hitPatterns = new List<TaikoRepeatingHitPatterns>();
            TaikoRepeatingHitPatterns? current = null;

            for (int i = 0; i < data.Count; i++)
            {
                current = new TaikoRepeatingHitPatterns(current);
                bool isCoupled = i < data.Count - 2 && data[i].IsRepetitionOf(data[i + 2]);

                if (!isCoupled)
                {
                    current.AlternatingMonoPatterns.Add(data[i]);
                }
                else
                {
                    while (isCoupled)
                    {
                        current.AlternatingMonoPatterns.Add(data[i]);
                        i++;
                        isCoupled = i < data.Count - 2 && data[i].IsRepetitionOf(data[i + 2]);
                    }

                    current.AlternatingMonoPatterns.Add(data[i]);
                    current.AlternatingMonoPatterns.Add(data[i + 1]);
                    i++;
                }

                hitPatterns.Add(current);
            }

            foreach (TaikoRepeatingHitPatterns pattern in hitPatterns)
                pattern.FindRepetitionInterval();

            return hitPatterns;
        }
    }

    private sealed class TaikoRhythmData
    {
        private static readonly double[] CommonRatios = { 1, 2, 0.5, 3, 1.0 / 3, 1.5, 2.0 / 3, 1.25, 0.8 };

        public TaikoRhythmData(TaikoDifficultyHitObject current)
        {
            var previous = current.Previous(0);
            Ratio = previous == null ? 1 : CommonRatios.MinBy(r => Math.Abs(r - current.DeltaTime / previous.DeltaTime));
        }

        public TaikoSameRhythmGroup? SameRhythmGroupedHitObjects;
        public TaikoSamePatternGroup? SamePatternsGroupedHitObjects;
        public double Ratio { get; }
    }

    private sealed class TaikoSameRhythmGroup : IHasInterval
    {
        public TaikoSameRhythmGroup(TaikoSameRhythmGroup? previous, List<TaikoDifficultyHitObject> hitObjects)
        {
            Previous = previous;
            HitObjects = hitObjects;
            List<double> deltas = hitObjects.Skip(1).Select(o => o.DeltaTime).ToList();
            if (deltas.Count > 0)
            {
                double modalDelta = Math.Round(deltas[0]);
                HitObjectInterval = previous?.HitObjectInterval is double p && Math.Abs(modalDelta - p) <= 5 ? p : modalDelta;
            }

            HitObjectIntervalRatio = previous?.HitObjectInterval is double previousInterval && HitObjectInterval is double currentInterval ? currentInterval / previousInterval : 1.0;

            if (previous != null)
                Interval = Math.Abs(StartTime - previous.StartTime) <= 5 ? 0 : StartTime - previous.StartTime;
        }

        public List<TaikoDifficultyHitObject> HitObjects { get; }
        public TaikoDifficultyHitObject FirstHitObject => HitObjects[0];
        public TaikoSameRhythmGroup? Previous { get; }
        public double StartTime => HitObjects[0].StartTime;
        public double Duration => HitObjects[^1].StartTime - HitObjects[0].StartTime;
        public double? HitObjectInterval { get; }
        public double HitObjectIntervalRatio { get; }
        public double Interval { get; } = double.PositiveInfinity;
    }

    private sealed class TaikoSamePatternGroup : IHasInterval
    {
        public TaikoSamePatternGroup(TaikoSamePatternGroup? previous, List<TaikoSameRhythmGroup> groups)
        {
            Previous = previous;
            Groups = groups;
        }

        public List<TaikoSameRhythmGroup> Groups { get; }
        public TaikoSamePatternGroup? Previous { get; }
        public double GroupInterval => Groups.Count > 1 ? Groups[1].Interval : Groups[0].Interval;
        public double IntervalRatio => Previous == null ? 1.0 : GroupInterval / Previous.GroupInterval;
        public TaikoDifficultyHitObject FirstHitObject => Groups[0].FirstHitObject;
        public IEnumerable<TaikoDifficultyHitObject> AllHitObjects => Groups.SelectMany(g => g.HitObjects);
        public double Interval => GroupInterval;
    }

    private static class TaikoRhythmPreprocessor
    {
        public static void ProcessAndAssign(List<TaikoDifficultyHitObject> hitObjects)
        {
            List<TaikoSameRhythmGroup> rhythmGroups = GroupByInterval(hitObjects).Aggregate(new List<TaikoSameRhythmGroup>(), (list, grouped) =>
            {
                list.Add(new TaikoSameRhythmGroup(list.LastOrDefault(), grouped));
                return list;
            });

            foreach (TaikoSameRhythmGroup rhythmGroup in rhythmGroups)
            foreach (TaikoDifficultyHitObject hitObject in rhythmGroup.HitObjects)
                hitObject.RhythmData.SameRhythmGroupedHitObjects = rhythmGroup;

            List<TaikoSamePatternGroup> patternGroups = GroupByInterval(rhythmGroups).Aggregate(new List<TaikoSamePatternGroup>(), (list, grouped) =>
            {
                list.Add(new TaikoSamePatternGroup(list.LastOrDefault(), grouped));
                return list;
            });

            foreach (TaikoSamePatternGroup patternGroup in patternGroups)
            foreach (TaikoDifficultyHitObject hitObject in patternGroup.AllHitObjects)
                hitObject.RhythmData.SamePatternsGroupedHitObjects = patternGroup;
        }
    }

    private static List<List<T>> GroupByInterval<T>(IReadOnlyList<T> objects) where T : IHasInterval
    {
        var groups = new List<List<T>>();
        int i = 0;
        while (i < objects.Count)
        {
            var grouped = new List<T> { objects[i] };
            i++;

            for (; i < objects.Count - 1; i++)
            {
                if (!AlmostEquals(objects[i].Interval, objects[i + 1].Interval, 5))
                {
                    if (objects[i + 1].Interval > objects[i].Interval + 5)
                    {
                        grouped.Add(objects[i]);
                        i++;
                    }

                    groups.Add(grouped);
                    goto next;
                }

                grouped.Add(objects[i]);
            }

            if (objects.Count > 2 && i < objects.Count && AlmostEquals(objects[^1].Interval, objects[^2].Interval, 5))
            {
                grouped.Add(objects[i]);
                i++;
            }

            groups.Add(grouped);
        next: ;
        }

        return groups;
    }

    private static double TaikoGreatHitWindow(double overallDifficulty) => Math.Floor(DifficultyRange(overallDifficulty, 50, 35, 20)) - 0.5;
}
