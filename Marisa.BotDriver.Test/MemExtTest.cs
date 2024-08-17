using Marisa.Backend.Shared;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

public class MemExtTest
{
    [Test]
    [TestCase("abc", "abc", StringComparison.Ordinal, true)]
    [TestCase("master1", "Master", StringComparison.OrdinalIgnoreCase, true)]
    public void Test_StartsWith(string s1, string s2, StringComparison cmp, bool res)
    {
        var oup = s1.AsMemory().StartsWith(s2, cmp);
        Assert.That(res == oup);
    }
}