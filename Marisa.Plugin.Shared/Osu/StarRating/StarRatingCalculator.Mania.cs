namespace Marisa.Plugin.Shared.Osu;

public static partial class StarRatingCalculator
{
    private sealed class ManiaObject : OsuHitObject
    {
        public ManiaObject(Vector2 position, double startTime, double endTime, int column)
            : base(position, startTime)
        {
            EndTimeValue = endTime;
            Column = column;
        }

        public int Column { get; }
        public double EndTimeValue { get; }
        public override double EndTime => EndTimeValue;
    }

    private sealed class ManiaDifficultyCalculator
    {
        private readonly Beatmap beatmap;
        private readonly double clockRate;

        public ManiaDifficultyCalculator(Beatmap beatmap, double clockRate)
        {
            this.beatmap = beatmap;
            this.clockRate = clockRate;
        }

        public double CalculateStarRating()
        {
            List<ManiaObject> objects = beatmap.HitObjects.OfType<ManiaObject>()
                                                .OrderBy(o => Math.Round(o.StartTime))
                                                .ToList();
            if (objects.Count == 0)
                return 0;

            int columns = Math.Max(1, (int)Math.Round(beatmap.Difficulty.CircleSize));
            var difficultyObjects = new List<ManiaDifficultyHitObject>();
            var perColumn = Enumerable.Range(0, columns).Select(_ => new List<ManiaDifficultyHitObject>()).ToArray();

            for (int i = 1; i < objects.Count; i++)
            {
                var difficultyObject = new ManiaDifficultyHitObject(objects[i], objects[i - 1], difficultyObjects, perColumn, difficultyObjects.Count, clockRate);
                difficultyObjects.Add(difficultyObject);
                perColumn[difficultyObject.Column].Add(difficultyObject);
            }

            var strain = new ManiaStrainSkill(columns);
            foreach (ManiaDifficultyHitObject difficultyObject in difficultyObjects)
                strain.Process(difficultyObject);

            return strain.DifficultyValue() * 0.018;
        }
    }

    private sealed class ManiaDifficultyHitObject : SimpleDifficultyHitObject
    {
        private readonly List<ManiaDifficultyHitObject>[] perColumnObjects;
        private readonly int columnIndex;

        public ManiaDifficultyHitObject(ManiaObject hitObject, ManiaObject lastObject, List<ManiaDifficultyHitObject> objects, List<ManiaDifficultyHitObject>[] perColumnObjects, int index, double clockRate)
            : base(hitObject, lastObject, objects.Cast<SimpleDifficultyHitObject>().ToList(), index, clockRate)
        {
            BaseManiaObject = hitObject;
            this.perColumnObjects = perColumnObjects;
            Column = hitObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[perColumnObjects.Length];
            ColumnStrainTime = StartTime - (PrevInColumn(0)?.StartTime ?? 0);

            if (index > 0)
            {
                ManiaDifficultyHitObject previous = objects[index - 1];
                for (int i = 0; i < previous.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = previous.PreviousHitObjects[i];

                PreviousHitObjects[previous.Column] = previous;
            }
        }

        public ManiaObject BaseManiaObject { get; }
        public int Column { get; }
        public ManiaDifficultyHitObject?[] PreviousHitObjects { get; }
        public double ColumnStrainTime { get; }

        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? perColumnObjects[Column][index] : null;
        }
    }

    private sealed class ManiaStrainSkill : SimpleStrainDecaySkill
    {
        private readonly double[] individualStrains;
        private double highestIndividualStrain;
        private double overallStrain = 1;

        public ManiaStrainSkill(int columns)
            : base(1, 1)
        {
            individualStrains = new double[columns];
        }

        protected override double StrainValueOf(SimpleDifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            individualStrains[maniaCurrent.Column] = ApplyDecay(individualStrains[maniaCurrent.Column], maniaCurrent.ColumnStrainTime, 0.125);
            individualStrains[maniaCurrent.Column] += EvaluateManiaIndividual(maniaCurrent);
            highestIndividualStrain = maniaCurrent.DeltaTime <= 1 ? Math.Max(highestIndividualStrain, individualStrains[maniaCurrent.Column]) : individualStrains[maniaCurrent.Column];

            overallStrain = ApplyDecay(overallStrain, maniaCurrent.DeltaTime, 0.30);
            overallStrain += EvaluateManiaOverall(maniaCurrent);

            return highestIndividualStrain + overallStrain - CurrentStrain;
        }

        protected override double CalculateInitialStrain(double time, SimpleDifficultyHitObject current)
        {
            return ApplyDecay(highestIndividualStrain, time - current.Previous(0)!.StartTime, 0.125)
                   + ApplyDecay(overallStrain, time - current.Previous(0)!.StartTime, 0.30);
        }

        private static double EvaluateManiaIndividual(ManiaDifficultyHitObject current)
        {
            double holdFactor = 1.0;
            foreach (ManiaDifficultyHitObject? previous in current.PreviousHitObjects)
            {
                if (previous != null && DefinitelyBigger(previous.EndTime, current.EndTime, 1) && DefinitelyBigger(current.StartTime, previous.StartTime, 1))
                {
                    holdFactor = 1.25;
                    break;
                }
            }

            return 2.0 * holdFactor;
        }

        private static double EvaluateManiaOverall(ManiaDifficultyHitObject current)
        {
            bool isOverlapping = false;
            double closestEndTime = Math.Abs(current.EndTime - current.StartTime);
            double holdFactor = 1.0;
            double holdAddition = 0;

            foreach (ManiaDifficultyHitObject? previous in current.PreviousHitObjects)
            {
                if (previous == null)
                    continue;

                isOverlapping |= DefinitelyBigger(previous.EndTime, current.StartTime, 1)
                                 && DefinitelyBigger(current.EndTime, previous.EndTime, 1)
                                 && DefinitelyBigger(current.StartTime, previous.StartTime, 1);

                if (DefinitelyBigger(previous.EndTime, current.EndTime, 1) && DefinitelyBigger(current.StartTime, previous.StartTime, 1))
                    holdFactor = 1.25;

                closestEndTime = Math.Min(closestEndTime, Math.Abs(current.EndTime - previous.EndTime));
            }

            if (isOverlapping)
                holdAddition = Logistic(closestEndTime, multiplier: 0.27, midpointOffset: 30);

            return (1 + holdAddition) * holdFactor;
        }
    }
}
