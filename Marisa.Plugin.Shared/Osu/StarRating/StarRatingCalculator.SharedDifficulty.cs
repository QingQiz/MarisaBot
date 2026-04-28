namespace Marisa.Plugin.Shared.Osu;

public static partial class StarRatingCalculator
{
    private class SimpleDifficultyHitObject
    {
        private readonly IReadOnlyList<SimpleDifficultyHitObject> objects;

        public SimpleDifficultyHitObject(OsuHitObject hitObject, OsuHitObject lastObject, IReadOnlyList<SimpleDifficultyHitObject> objects, int index, double clockRate = 1)
        {
            this.objects = objects;
            BaseObject = hitObject;
            LastObject = lastObject;
            Index = index;
            DeltaTime = (hitObject.StartTime - lastObject.StartTime) / clockRate;
            StartTime = hitObject.StartTime / clockRate;
            EndTime = hitObject.EndTime / clockRate;
        }

        public OsuHitObject BaseObject { get; }
        public OsuHitObject LastObject { get; }
        public int Index { get; }
        public double DeltaTime { get; }
        public double StartTime { get; }
        public double EndTime { get; }

        public SimpleDifficultyHitObject? Previous(int backwardsIndex)
        {
            int index = Index - (backwardsIndex + 1);
            return index >= 0 && index < objects.Count ? objects[index] : null;
        }
    }

    private abstract class SimpleStrainSkill
    {
        private readonly int sectionLength;
        private readonly double decayWeight;
        private double currentSectionPeak;
        private double currentSectionEnd;
        private readonly List<double> strainPeaks = new();
        protected readonly List<double> ObjectStrains = new();

        protected SimpleStrainSkill(int sectionLength = 400, double decayWeight = 0.9)
        {
            this.sectionLength = sectionLength;
            this.decayWeight = decayWeight;
        }

        public void Process(SimpleDifficultyHitObject current)
        {
            if (current.Index == 0)
                currentSectionEnd = Math.Ceiling(current.StartTime / sectionLength) * sectionLength;

            while (current.StartTime > currentSectionEnd)
            {
                strainPeaks.Add(currentSectionPeak);
                currentSectionPeak = CalculateInitialStrain(currentSectionEnd, current);
                currentSectionEnd += sectionLength;
            }

            double strain = StrainValueAt(current);
            currentSectionPeak = Math.Max(strain, currentSectionPeak);
            ObjectStrains.Add(strain);
        }

        public IEnumerable<double> CurrentStrainPeaksPublic() => strainPeaks.Append(currentSectionPeak);

        public IEnumerable<double> ObjectStrainsPublic() => ObjectStrains;

        public virtual double DifficultyValue()
        {
            double difficulty = 0;
            double weight = 1;
            foreach (double strain in CurrentStrainPeaksPublic().Where(p => p > 0).OrderByDescending(p => p))
            {
                difficulty += strain * weight;
                weight *= decayWeight;
            }

            return difficulty;
        }

        public double CountTopWeightedStrains()
        {
            if (ObjectStrains.Count == 0)
                return 0;

            double consistentTopStrain = DifficultyValue() / 10;
            if (consistentTopStrain == 0)
                return ObjectStrains.Count;

            return ObjectStrains.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }

        protected abstract double StrainValueAt(SimpleDifficultyHitObject current);
        protected abstract double CalculateInitialStrain(double time, SimpleDifficultyHitObject current);
    }

    private abstract class SimpleStrainDecaySkill : SimpleStrainSkill
    {
        private readonly double skillMultiplier;
        private readonly double strainDecayBase;
        protected double CurrentStrain { get; private set; }

        protected SimpleStrainDecaySkill(double skillMultiplier, double strainDecayBase, int sectionLength = 400, double decayWeight = 0.9)
            : base(sectionLength, decayWeight)
        {
            this.skillMultiplier = skillMultiplier;
            this.strainDecayBase = strainDecayBase;
        }

        protected override double CalculateInitialStrain(double time, SimpleDifficultyHitObject current) => CurrentStrain * ApplyDecay(1, time - current.Previous(0)!.StartTime, strainDecayBase);

        protected override double StrainValueAt(SimpleDifficultyHitObject current)
        {
            CurrentStrain = ApplyDecay(CurrentStrain, current.DeltaTime, strainDecayBase);
            CurrentStrain += StrainValueOf(current) * skillMultiplier;
            return CurrentStrain;
        }

        protected abstract double StrainValueOf(SimpleDifficultyHitObject current);

        protected static double ApplyDecay(double value, double deltaTime, double decayBase) => value * Math.Pow(decayBase, deltaTime / 1000);
    }

    private static bool DefinitelyBigger(double value1, double value2, double acceptableDifference = 0) => value1 - value2 > acceptableDifference;

    private static double NormValue(double p, params double[] values) => Math.Pow(values.Sum(x => Math.Pow(x, p)), 1 / p);

    private static double LogisticExponent(double exponent, double maxValue = 1) => maxValue / (1 + Math.Exp(exponent));

    private static double BellCurveValue(double x, double mean, double width, double multiplier = 1.0) => multiplier * Math.Exp(Math.E * -(Math.Pow(x - mean, 2) / Math.Pow(width, 2)));
}
