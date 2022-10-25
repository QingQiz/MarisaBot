using System;
using System.IO;
using System.Threading.Tasks;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Configuration;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmTest
{
    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [Test]
    [TestCase(101_0000, 14.9, 17.05)]
    [TestCase(100_7777, 14.9, 16.92)]
    [TestCase(100_6666, 14.9, 16.73)]
    [TestCase(100_3333, 14.9, 16.23)]
    [TestCase(97_5555, 14.9, 14.92)]
    [TestCase(92_5555, 14.9, 11.92)]
    [TestCase(92_2222, 14.9, 11.67)]
    [TestCase(88_8888, 14.9, 9.34)]
    [TestCase(66_6666, 14.9, 2.74)]
    [TestCase(44_4444, 14.9, 0)]
    public void Rating_Calculator(int ach, double constant, double expected)
    {
        var actual = ChunithmSong.Ra(ach, constant);
        Assert.AreEqual(expected, actual);
    }


    [Test]
    [TestCase(14.9, 923456, 923500)]
    [TestCase(14.9, 500000, 500607)]
    [TestCase(14.9, 0, 500607)]
    public void Binary_Search_NextRa(double constant, int ach, int nextAch)
    {
        var actual = ChunithmSong.NextRa(ach, constant);
        Assert.AreEqual(nextAch, actual);
    }

    [Test]
    public void Should_Draw_Score_Card()
    {
        var score = new ChunithmScore
        {
            Id         = 335,
            CId        = 335,
            Constant   = 13.9,
            Rating     = 16.0,
            Fc         = "",
            Level      = "13+",
            LevelIndex = 3,
            LevelLabel = "Expert",
            Score      = 1009000,
            Title      = "这是个名字名字名字名字名字名字名字名字名字名字",
        };

        score.Draw().Show();

        Assert.Pass();
    }

    [Test]
    public async Task Should_Draw_B30()
    {
        var rating = await Chunithm.Chunithm.GetRating(null, 2435865554);

        rating.Draw().Show();

        Assert.Pass();
    }
}