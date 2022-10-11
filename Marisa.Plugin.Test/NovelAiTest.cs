using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class NovelAiTest
{
    [Test]
    [TestCase("miku, small, snow", "green")]
    public async Task Text2Image(string s1, string s2)
    {
        var x = await Ai.Ai.Txt2Img(s1, s2, 1);
        Console.WriteLine("data:image/png;base64," + x);
    }

    [Test]
    [TestCase("marisa", "green")]
    public async Task Image2Image(string s1, string s2)
    {
        var x = await Ai.Ai.Txt2Img(s1, s2, 1);
        var y = await Ai.Ai.Img2Img(s1, s2, x, 1);
        Console.WriteLine("data:image/png;base64," + y);
    }
}