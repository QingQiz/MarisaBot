using System.Reflection;
using Marisa.Plugin.Chunithm;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
public class ChunithmSummaryFlagTest
{
    private static readonly MethodInfo ParseSummaryFlags =
        typeof(ChunithmChartFilter).Assembly.GetType("Marisa.Plugin.Chunithm.Chunithm")!
            .GetMethod("ParseSummaryFlags", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo SummaryTitle =
        typeof(ChunithmChartFilter).Assembly.GetType("Marisa.Plugin.Chunithm.Chunithm")!
            .GetMethod("SummaryTitle", BindingFlags.NonPublic | BindingFlags.Static)!;

    [TestCase("level", "14+", ChunithmChartFilter.None, "level 14+ 完成表")]
    [TestCase("level", "14+", ChunithmChartFilter.Ss, "level 14+ SS~SSS+ 完成表")]
    [TestCase("level", "14+", ChunithmChartFilter.Ss | ChunithmChartFilter.NotSssPlus, "level 14+ SS~SSS 完成表")]
    [TestCase("level", "14+", ChunithmChartFilter.SssPlus, "level 14+ SSS+ 完成表")]
    [TestCase("level", "14+", ChunithmChartFilter.NotSs, "level 14+ 未SS 完成表")]
    [TestCase("genre", "ORIGINAL", ChunithmChartFilter.OverPower, "genre ORIGINAL OP对象 完成表")]
    [TestCase("base", "14.0-14.5", ChunithmChartFilter.OverPower | ChunithmChartFilter.Ss,
        "base 14.0-14.5 OP对象SS~SSS+ 完成表")]
    [TestCase("level", "15", ChunithmChartFilter.FullCombo | ChunithmChartFilter.NotAllJustice, "level 15 FC 完成表")]
    [TestCase("level", "15", ChunithmChartFilter.NotFullCombo, "level 15 未FC 完成表")]
    [TestCase("level", "15", ChunithmChartFilter.AllJusticeCritical | ChunithmChartFilter.NotAllJustice,
        "level 15 无 完成表")]
    [TestCase("version", "CHUNITHM XVERSEX", ChunithmChartFilter.AllJusticeCritical | ChunithmChartFilter.Ss,
        "version CHUNITHM XVERSEX AJCSS~SSS+ 完成表")]
    public void TitleDescribesCategoryArgumentAndFilter(
        string category, string argument, ChunithmChartFilter filter, string expected)
    {
        Assert.That(SummaryTitle.Invoke(null, [category, argument, filter]), Is.EqualTo(expected));
    }

    [TestCase("13.0", "", ChunithmChartFilter.None, "13.0")]
    [TestCase("13.0 --op", "", ChunithmChartFilter.OverPower, "13.0")]
    [TestCase("13.0 --fc", "", ChunithmChartFilter.FullCombo, "13.0")]
    [TestCase("13.0 --aj", "", ChunithmChartFilter.AllJustice, "13.0")]
    [TestCase("13.0 --ajc", "", ChunithmChartFilter.AllJusticeCritical, "13.0")]
    [TestCase("13.0 --!fc", "", ChunithmChartFilter.NotFullCombo, "13.0")]
    [TestCase("13.0 --!aj", "", ChunithmChartFilter.NotAllJustice, "13.0")]
    [TestCase("13.0 --!ajc", "", ChunithmChartFilter.NotAllJusticeCritical, "13.0")]
    [TestCase("13.0 --ss", "", ChunithmChartFilter.Ss, "13.0")]
    [TestCase("13.0 --ss+", "", ChunithmChartFilter.SsPlus, "13.0")]
    [TestCase("13.0 --sss", "", ChunithmChartFilter.Sss, "13.0")]
    [TestCase("13.0 --sss+", "", ChunithmChartFilter.SssPlus, "13.0")]
    [TestCase("13.0 --!ss", "", ChunithmChartFilter.NotSs, "13.0")]
    [TestCase("13.0 --!ss+", "", ChunithmChartFilter.NotSsPlus, "13.0")]
    [TestCase("13.0 --!sss", "", ChunithmChartFilter.NotSss, "13.0")]
    [TestCase("13.0 --!sss+", "", ChunithmChartFilter.NotSssPlus, "13.0")]
    [TestCase("14+ --ss+ --!sss", "", ChunithmChartFilter.SsPlus | ChunithmChartFilter.NotSss, "14+")]
    [TestCase("13.0 --sss --!sss+", "", ChunithmChartFilter.Sss | ChunithmChartFilter.NotSssPlus, "13.0")]
    [TestCase("--!fc", "", ChunithmChartFilter.NotFullCombo, "")]
    [TestCase("13.0 --fc --!aj", "", ChunithmChartFilter.FullCombo | ChunithmChartFilter.NotAllJustice, "13.0")]
    [TestCase("13.0 --!ajc --aj", "", ChunithmChartFilter.NotAllJusticeCritical | ChunithmChartFilter.AllJustice, "13.0")]
    [TestCase("13.0 -d --!fc --op", "desc",
        ChunithmChartFilter.NotFullCombo | ChunithmChartFilter.OverPower, "13.0")]
    [TestCase("13.0-13.5 --op -a", "asc", ChunithmChartFilter.OverPower, "13.0-13.5")]
    [TestCase("ORIGINAL --fc", "", ChunithmChartFilter.FullCombo, "ORIGINAL")]
    [TestCase("14+ --!aj", "", ChunithmChartFilter.NotAllJustice, "14+")]
    public void SuffixIsSplitFromArguments(
        string command, string sort, ChunithmChartFilter filter, string rest)
    {
        var args   = new object[] { command };
        var parsed = ((string, ChunithmChartFilter))ParseSummaryFlags.Invoke(null, args)!;

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Item1, Is.EqualTo(sort));
            Assert.That(parsed.Item2, Is.EqualTo(filter));
            Assert.That((string)args[0], Is.EqualTo(rest));
        });
    }
}
