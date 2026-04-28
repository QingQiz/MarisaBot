using System;
using System.IO;
using System.Linq;
using Marisa.Plugin.Shared.Osu;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class StarRatingCalculatorTest
{
    private const double Tolerance = 0.00001;

    [TestCase("Osu/diffcalc-test.osu", 6.6232533278125061d)]
    [TestCase("Osu/zero-length-sliders.osu", 1.5045783545699611d)]
    [TestCase("Osu/very-fast-slider.osu", 0.43333836671191595d)]
    [TestCase("Osu/nan-slider.osu", 0.13841532030395723d)]
    [TestCase("Mania/diffcalc-test.osu", 2.3493769750220914d)]
    [TestCase("Catch/diffcalc-test.osu", 4.0505463516206195d)]
    [TestCase("Taiko/diffcalc-test.osu", 3.3190848563395079d)]
    [TestCase("Taiko/diffcalc-test-strong.osu", 3.3190848563395079d)]
    public void MatchesUpstreamDifficultyCase(string beatmapPath, double expectedStarRating)
    {
        string beatmapText = File.ReadAllText(GetResourcePath(beatmapPath));

        double actualStarRating = StarRatingCalculator.Calculate(beatmapText);

        Assert.That(actualStarRating, Is.InRange(expectedStarRating - Tolerance, expectedStarRating + Tolerance));
    }

    [TestCase("Osu/diffcalc-test.osu", 9.6491691624112761d, "DT")]
    [TestCase("Osu/zero-length-sliders.osu", 1.756936832498702d, "DT")]
    [TestCase("Osu/very-fast-slider.osu", 0.57771197086735004d, "DT")]
    [TestCase("Osu/diffcalc-test.osu", 6.6232533278125061d, "CL")]
    [TestCase("Osu/zero-length-sliders.osu", 1.5045783545699611d, "CL")]
    [TestCase("Osu/very-fast-slider.osu", 0.43333836671191595d, "CL")]
    [TestCase("Mania/diffcalc-test.osu", 2.797245912537965d, "DT")]
    [TestCase("Catch/diffcalc-test.osu", 5.1696411260785498d, "DT")]
    [TestCase("Taiko/diffcalc-test.osu", 4.4551414906554987d, "DT")]
    [TestCase("Taiko/diffcalc-test-strong.osu", 4.4551414906554987d, "DT")]
    public void MatchesUpstreamDifficultyModCase(string beatmapPath, double expectedStarRating, string mod)
    {
        string beatmapText = File.ReadAllText(GetResourcePath(beatmapPath));

        double actualStarRating = StarRatingCalculator.Calculate(beatmapText, mod);

        Assert.That(actualStarRating, Is.InRange(expectedStarRating - Tolerance, expectedStarRating + Tolerance));
    }

    [TestCase("DT=1.25", "DT:1.25", "DT1.25")]
    [TestCase("NC=1.25", "NC:1.25", "NC1.25")]
    [TestCase("HT=0.80", "HT:0.80", "HT0.80")]
    [TestCase("DC=0.80", "DC:0.80", "DC0.80")]
    public void SupportsVariantRateModSyntax(params string[] equivalentMods)
    {
        string beatmapText = File.ReadAllText(GetResourcePath("Osu/diffcalc-test.osu"));
        double expectedStarRating = StarRatingCalculator.Calculate(beatmapText, equivalentMods[0]);

        foreach (string mod in equivalentMods.Skip(1))
            Assert.That(StarRatingCalculator.Calculate(beatmapText, mod), Is.InRange(expectedStarRating - Tolerance, expectedStarRating + Tolerance));
    }

    [TestCase("DT=1.00")]
    [TestCase("DT=2.01")]
    [TestCase("HT=0.49")]
    [TestCase("HT=1.00")]
    public void RejectsOutOfRangeRateModSpeed(string mod)
    {
        string beatmapText = File.ReadAllText(GetResourcePath("Osu/diffcalc-test.osu"));

        Assert.Throws<NotSupportedException>(() => StarRatingCalculator.Calculate(beatmapText, mod));
    }

    private static string GetResourcePath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "Resources", "Testing", "Beatmaps", relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
