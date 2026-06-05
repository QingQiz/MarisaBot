using Marisa.Plugin.Shared.Osu;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class OsuCommandParserTest
{
    [TestCase("", "", null, null)]
    [TestCase("  ", "", null, null)]
    [TestCase("42", "", 42, null)]
    [TestCase("999", "999", null, null)]
    [TestCase("laplaze", "laplaze", null, null)]
    [TestCase("!!!", "!!!", null, null)]
    public void Parse_Basic(string input, string name, int? rankStart, int? mode)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.That(r!.Name, Is.EqualTo(name));
        Assert.That(r.Rank?.Start, Is.EqualTo(rankStart));
        Assert.That(r.Rank?.End, Is.Null);
        Assert.That(r.Mode, Is.EqualTo(mode));
    }

    [TestCase("  laplaze  ", "laplaze")]
    [TestCase("laplaze [abc]", "laplaze [abc]")]
    [TestCase("laplaze-test", "laplaze-test")]
    public void Parse_NameEdgeCases(string input, string name)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.That(r!.Name, Is.EqualTo(name));
    }

    [TestCase("#42", "", 42, null)]
    [TestCase("#1-5", "", 1, 5)]
    [TestCase("laplaze#42", "laplaze", 42, null)]
    [TestCase("laplaze#1-5", "laplaze", 1, 5)]
    public void Parse_WithRank(string input, string name, int rankStart, int? rankEnd)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(r!.Name, Is.EqualTo(name));
            Assert.That(r.Rank?.Start, Is.EqualTo(rankStart));
            Assert.That(r.Rank?.End, Is.EqualTo(rankEnd));
            Assert.That(r.Mode, Is.Null);
        });
    }

    [TestCase("#0")]
    [TestCase("#201")]
    [TestCase("#1-201")]
    [TestCase("#5-3")]
    public void Parse_InvalidRank_ReturnsNullRank(string input)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.That(r!.Rank, Is.Null);
    }

    [TestCase(":0", 0)]
    [TestCase(":osu", 0)]
    [TestCase(":taiko", 1)]
    [TestCase(":fruit", 2)]
    [TestCase(":mania", 3)]
    [TestCase("：0", 0)]
    [TestCase("laplaze:0", 0)]
    [TestCase("laplaze:taiko", 1)]
    public void Parse_WithMode(string input, int mode)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.That(r!.Mode, Is.EqualTo(mode));
    }

    [TestCase("laplaze:5")]
    [TestCase("laplaze:unknown")]
    public void Parse_InvalidMode_ReturnsNullMode(string input)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(r!.Name, Is.EqualTo("laplaze"));
            Assert.That(r.Rank, Is.Null);
            Assert.That(r.Mode, Is.Null);
        });
    }

    [TestCase("laplaze#42:0", "laplaze", 42, null, 0)]
    [TestCase("laplaze#1-5:0", "laplaze", 1, 5, 0)]
    public void Parse_Full(string input, string name, int rankStart, int? rankEnd, int mode)
    {
        var r = OsuCommandParser.Parse(input);
        Assert.That(r, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(r!.Name, Is.EqualTo(name));
            Assert.That(r.Rank?.Start, Is.EqualTo(rankStart));
            Assert.That(r.Rank?.End, Is.EqualTo(rankEnd));
            Assert.That(r.Mode, Is.EqualTo(mode));
        });
    }
}
