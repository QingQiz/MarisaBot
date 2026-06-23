using System.Collections.Generic;
using Marisa.Plugin.Shared.Chunithm;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmVersionTest
{
    [TestCase("CHUNITHM VERSE", true)]
    [TestCase("CHUNITHM LUMINOUS PLUS", true)]    // 国服当前版本(2026)涵盖 LUMINOUS PLUS + VERSE 两版
    [TestCase("CHUNITHM LUMINOUS", false)]
    [TestCase("CHUNITHM SUN PLUS", false)]
    [TestCase("CHUNITHM NEW", false)]
    [TestCase("CHUNITHM", false)]
    [TestCase("unknown version", false)]
    [TestCase(null, false)]
    public void IsCurrent_Boundaries(string? name, bool expected)
    {
        Assert.That(ChunithmVersion.IsCurrent(name), Is.EqualTo(expected));
    }

    // 当前版本涵盖 LUMINOUS PLUS 与 VERSE 两版（国服中二节奏 2026）。
    [Test]
    public void CurrentSpansLuminousPlusAndVerse()
    {
        var current = new HashSet<string> { "CHUNITHM LUMINOUS PLUS", "CHUNITHM VERSE" };
        foreach (var name in ChunithmVersion.NameToCode.Keys)
        {
            Assert.That(ChunithmVersion.IsCurrent(name), Is.EqualTo(current.Contains(name)), $"mismatch for {name}");
        }
    }
}
