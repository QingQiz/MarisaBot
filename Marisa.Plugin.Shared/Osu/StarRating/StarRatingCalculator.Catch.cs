namespace Marisa.Plugin.Shared.Osu;

public static partial class StarRatingCalculator
{
    private class CatchPalpableObject : OsuHitObject
    {
        public CatchPalpableObject(Vector2 position, double startTime)
            : base(position, startTime)
        {
        }

        public float EffectiveX => Math.Clamp(Position.X + XOffset, 0, 512);
        public float XOffset { get; set; }
        public float DistanceToHyperDash { get; set; }
        public bool HyperDash { get; set; }
    }

    private sealed class CatchFruit : CatchPalpableObject
    {
        public CatchFruit(Vector2 position, double startTime)
            : base(position, startTime)
        {
        }
    }

    private sealed class CatchDroplet : CatchPalpableObject
    {
        public CatchDroplet(Vector2 position, double startTime, bool tiny)
            : base(position, startTime)
        {
            Tiny = tiny;
        }

        public bool Tiny { get; }
    }

    private sealed class CatchJuiceStream : OsuHitObject
    {
        public CatchJuiceStream(Vector2 position, double startTime, SliderPath path, int repeatCount)
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
        public List<CatchPalpableObject> NestedObjects { get; private set; } = new();

        public void BuildNestedObjects()
        {
            NestedObjects = new List<CatchPalpableObject>();
            SliderEventGenerator.SliderEvent? lastEvent = null;

            foreach (SliderEventGenerator.SliderEvent e in SliderEventGenerator.Generate(StartTime, SpanDuration, Velocity, TickDistance, Path.Distance, (int)SpanCount))
            {
                if (lastEvent != null)
                {
                    double sinceLastTick = (int)e.Time - (int)lastEvent.Time;

                    if (sinceLastTick > 80)
                    {
                        double timeBetweenTiny = sinceLastTick;
                        while (timeBetweenTiny > 100)
                            timeBetweenTiny /= 2;

                        for (double t = timeBetweenTiny; t < sinceLastTick; t += timeBetweenTiny)
                        {
                            double progress = lastEvent.PathProgress + t / sinceLastTick * (e.PathProgress - lastEvent.PathProgress);
                            NestedObjects.Add(new CatchDroplet(new Vector2(EffectiveXAt(progress), Position.Y), t + lastEvent.Time, tiny: true));
                        }
                    }
                }

                lastEvent = e;

                switch (e.Type)
                {
                    case NestedSliderObjectType.Tick:
                        NestedObjects.Add(new CatchDroplet(new Vector2(EffectiveXAt(e.PathProgress), Position.Y), e.Time, tiny: false));
                        break;

                    case NestedSliderObjectType.Head:
                    case NestedSliderObjectType.Tail:
                    case NestedSliderObjectType.Repeat:
                        NestedObjects.Add(new CatchFruit(new Vector2(EffectiveXAt(e.PathProgress), Position.Y), e.Time));
                        break;
                }
            }
        }

        private float EffectiveXAt(double progress) => Math.Clamp(Position.X + Path.PositionAt(progress).X, 0, 512);
    }

    private sealed class CatchDifficultyCalculator
    {
        private readonly Beatmap beatmap;
        private readonly double clockRate;

        public CatchDifficultyCalculator(Beatmap beatmap, double clockRate)
        {
            this.beatmap = beatmap;
            this.clockRate = clockRate;
        }

        public double CalculateStarRating()
        {
            ApplyPositionOffsets(beatmap);

            List<CatchPalpableObject> palpableObjects = GetCatchPalpableObjects(beatmap).Where(o => o is not CatchDroplet { Tiny: true }).OrderBy(o => o.StartTime).ToList();
            if (palpableObjects.Count == 0)
                return 0;

            InitialiseHyperDash(palpableObjects, beatmap.Difficulty);

            float halfCatcherWidth = CalculateCatchWidth(beatmap.Difficulty) * 0.5f;
            halfCatcherWidth *= 1 - Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f;

            var difficultyObjects = new List<CatchDifficultyHitObject>();
            CatchPalpableObject? lastObject = null;

            foreach (CatchPalpableObject hitObject in palpableObjects)
            {
                if (hitObject is CatchDroplet { Tiny: true })
                    continue;

                if (lastObject != null)
                    difficultyObjects.Add(new CatchDifficultyHitObject(hitObject, lastObject, difficultyObjects, difficultyObjects.Count, halfCatcherWidth, clockRate));

                lastObject = hitObject;
            }

            var movement = new CatchMovementSkill(clockRate);
            foreach (CatchDifficultyHitObject difficultyObject in difficultyObjects)
                movement.Process(difficultyObject);

            return Math.Sqrt(movement.DifficultyValue()) * 4.59;
        }

        private static IEnumerable<CatchPalpableObject> GetCatchPalpableObjects(Beatmap beatmap)
        {
            foreach (OsuHitObject hitObject in beatmap.HitObjects.OrderBy(h => h.StartTime))
            {
                if (hitObject is CatchPalpableObject palpable)
                    yield return palpable;

                if (hitObject is CatchJuiceStream stream)
                {
                    foreach (CatchPalpableObject nested in stream.NestedObjects)
                        yield return nested;
                }
            }
        }

        private static void InitialiseHyperDash(List<CatchPalpableObject> objects, BeatmapDifficulty difficulty)
        {
            CatchPalpableObject[] palpable = objects.Where(h => h is CatchFruit || h is CatchDroplet { Tiny: false }).ToArray();
            double halfCatcherWidth = CalculateCatchWidth(difficulty) / 2;
            halfCatcherWidth /= 0.8;
            int lastDirection = 0;
            double lastExcess = halfCatcherWidth;

            for (int i = 0; i < palpable.Length - 1; i++)
            {
                CatchPalpableObject current = palpable[i];
                CatchPalpableObject next = palpable[i + 1];
                current.HyperDash = false;
                current.DistanceToHyperDash = 0;
                int thisDirection = next.EffectiveX > current.EffectiveX ? 1 : -1;
                double timeToNext = (int)next.StartTime - (int)current.StartTime - 1000f / 60f / 4;
                double distanceToNext = Math.Abs(next.EffectiveX - current.EffectiveX) - (lastDirection == thisDirection ? lastExcess : halfCatcherWidth);
                float distanceToHyper = (float)(timeToNext - distanceToNext);

                if (distanceToHyper < 0)
                {
                    current.HyperDash = true;
                    lastExcess = halfCatcherWidth;
                }
                else
                {
                    current.DistanceToHyperDash = distanceToHyper;
                    lastExcess = Math.Clamp(distanceToHyper, 0, halfCatcherWidth);
                }

                lastDirection = thisDirection;
            }
        }

        private static void ApplyPositionOffsets(Beatmap beatmap)
        {
            var rng = new LegacyRandom(1337);
            float? lastPosition = null;
            double lastStartTime = 0;

            foreach (OsuHitObject hitObject in beatmap.HitObjects.OrderBy(h => h.StartTime))
            {
                if (hitObject is CatchFruit fruit)
                    ApplyHardRockOffset(fruit, ref lastPosition, ref lastStartTime, rng, hardRockOffsets: false);
                else if (hitObject is CatchJuiceStream stream)
                {
                    lastPosition = stream.Position.X + stream.Path.ControlPoints[^1].Position.X;
                    lastStartTime = stream.StartTime;

                    foreach (CatchPalpableObject nested in stream.NestedObjects)
                    {
                        nested.XOffset = 0;

                        if (nested is CatchDroplet { Tiny: true })
                            nested.XOffset = Math.Clamp(rng.Next(-20, 20), -nested.Position.X, 512 - nested.Position.X);
                        else if (nested is CatchDroplet)
                            rng.Next();
                    }
                }
            }
        }

        private static void ApplyHardRockOffset(CatchPalpableObject hitObject, ref float? lastPosition, ref double lastStartTime, LegacyRandom rng, bool hardRockOffsets)
        {
            hitObject.XOffset = 0;

            if (!hardRockOffsets)
                return;

            float offsetPosition = hitObject.Position.X;
            double startTime = hitObject.StartTime;

            if (lastPosition == null || lastPosition == 0)
            {
                lastPosition = offsetPosition;
                lastStartTime = startTime;
                return;
            }

            float positionDiff = offsetPosition - lastPosition.Value;
            int timeDiff = (int)(startTime - lastStartTime);

            if (timeDiff > 1000)
            {
                lastPosition = offsetPosition;
                lastStartTime = startTime;
                return;
            }

            if (positionDiff == 0)
            {
                ApplyRandomOffset(ref offsetPosition, timeDiff / 4d, rng);
                hitObject.XOffset = offsetPosition - hitObject.Position.X;
                return;
            }

            if (Math.Abs(positionDiff) < timeDiff / 3)
                ApplyOffset(ref offsetPosition, positionDiff);

            hitObject.XOffset = offsetPosition - hitObject.Position.X;
            lastPosition = offsetPosition;
            lastStartTime = startTime;
        }

        private static void ApplyRandomOffset(ref float position, double maxOffset, LegacyRandom rng)
        {
            bool right = rng.NextBool();
            float rand = Math.Min(20, rng.Next(0, Math.Max(0, maxOffset)));

            if (right)
            {
                if (position + rand <= 512)
                    position += rand;
                else
                    position -= rand;
            }
            else
            {
                if (position - rand >= 0)
                    position -= rand;
                else
                    position += rand;
            }
        }

        private static void ApplyOffset(ref float position, float amount)
        {
            if (amount > 0)
            {
                if (position + amount < 512)
                    position += amount;
            }
            else
            {
                if (position + amount > 0)
                    position += amount;
            }
        }

        private static float CalculateCatchWidth(BeatmapDifficulty difficulty) => 106.75f * CalculateScaleFromCircleSize(difficulty.CircleSize, applyFudge: false) * 2 * 0.8f;
    }

    private sealed class CatchDifficultyHitObject : SimpleDifficultyHitObject
    {
        private const float NormalizedHalfCatcherWidth = 41.0f;
        private const float AbsolutePlayerPositioningError = 16.0f;

        public CatchDifficultyHitObject(CatchPalpableObject hitObject, CatchPalpableObject lastObject, List<CatchDifficultyHitObject> objects, int index, float halfCatcherWidth, double clockRate)
            : base(hitObject, lastObject, objects.Cast<SimpleDifficultyHitObject>().ToList(), index, clockRate)
        {
            BaseCatchObject = hitObject;
            LastCatchObject = lastObject;
            float scalingFactor = NormalizedHalfCatcherWidth / halfCatcherWidth;
            NormalizedPosition = hitObject.EffectiveX * scalingFactor;
            LastNormalizedPosition = lastObject.EffectiveX * scalingFactor;
            StrainTime = Math.Max(40, DeltaTime);
            SetMovementState();
        }

        public CatchPalpableObject BaseCatchObject { get; }
        public CatchPalpableObject LastCatchObject { get; }
        public float NormalizedPosition { get; }
        public float LastNormalizedPosition { get; }
        public float PlayerPosition { get; private set; }
        public float LastPlayerPosition { get; private set; }
        public float DistanceMoved { get; private set; }
        public float ExactDistanceMoved { get; private set; }
        public double StrainTime { get; }

        private void SetMovementState()
        {
            LastPlayerPosition = Index == 0 ? LastNormalizedPosition : ((CatchDifficultyHitObject)Previous(0)!).PlayerPosition;
            PlayerPosition = Math.Clamp(LastPlayerPosition, NormalizedPosition - (NormalizedHalfCatcherWidth - AbsolutePlayerPositioningError), NormalizedPosition + (NormalizedHalfCatcherWidth - AbsolutePlayerPositioningError));
            DistanceMoved = PlayerPosition - LastPlayerPosition;
            ExactDistanceMoved = NormalizedPosition - LastPlayerPosition;

            if (LastCatchObject.HyperDash)
                PlayerPosition = NormalizedPosition;
        }
    }

    private sealed class CatchMovementSkill : SimpleStrainDecaySkill
    {
        private readonly double clockRate;

        public CatchMovementSkill(double clockRate)
            : base(1, 0.2, sectionLength: 750, decayWeight: 0.94)
        {
            this.clockRate = clockRate;
        }

        protected override double StrainValueOf(SimpleDifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;
            var catchLast = (CatchDifficultyHitObject?)current.Previous(0);
            var catchLastLast = (CatchDifficultyHitObject?)current.Previous(1);
            double weightedStrainTime = catchCurrent.StrainTime + 13 + 3 / clockRate;
            double distanceAddition = Math.Pow(Math.Abs(catchCurrent.DistanceMoved), 1.3) / 510;
            double sqrtStrain = Math.Sqrt(weightedStrainTime);

            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1)
            {
                if (current.Index >= 1 && catchLast != null && Math.Abs(catchLast.DistanceMoved) > 0.1 && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
                {
                    double bonusFactor = Math.Min(50, Math.Abs(catchCurrent.DistanceMoved)) / 50;
                    double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(catchLast.DistanceMoved)) / 70, 0.38);
                    distanceAddition += 21.0 / Math.Sqrt(catchLast.StrainTime + 16) * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
                }

                distanceAddition += 12.5 * Math.Min(Math.Abs(catchCurrent.DistanceMoved), 82) / (41 * 6) / sqrtStrain;
            }

            if (catchCurrent.LastCatchObject.DistanceToHyperDash <= 20.0f)
            {
                double edgeDashBonus = catchCurrent.LastCatchObject.HyperDash ? 0 : 5.7;
                distanceAddition *= 1.0 + edgeDashBonus * ((20 - catchCurrent.LastCatchObject.DistanceToHyperDash) / 20) * Math.Pow(Math.Min(catchCurrent.StrainTime * clockRate, 265) / 265, 1.5);
            }

            if (current.Index >= 2 && catchLast != null && catchLastLast != null
                                   && Math.Abs(catchCurrent.ExactDistanceMoved) <= 82
                                   && catchCurrent.ExactDistanceMoved == -catchLast.ExactDistanceMoved
                                   && catchLast.ExactDistanceMoved == -catchLastLast.ExactDistanceMoved
                                   && catchCurrent.StrainTime == catchLast.StrainTime
                                   && catchLast.StrainTime == catchLastLast.StrainTime)
                distanceAddition = 0;

            return distanceAddition / weightedStrainTime;
        }
    }
}
