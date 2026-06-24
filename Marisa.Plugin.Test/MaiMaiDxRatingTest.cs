using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

// B50Ra 现采用 Diving-Fish maimaidx-prober 的 23 行系数表（含每段位临界次档）。
// 期望值按 floor(constant * min(100.5, achievement)/100 * coefficient) 手算核对。
public class MaiMaiDxRatingTest
{
    // 临界次档（.9999）此前被旧的粗粒度 switch 吞掉，现已修正。
    [TestCase(79.9999, 12.0, 122)]  // 旧值 115（吞 12.8 档）
    [TestCase(96.9999, 13.5, 230)]  // 旧值 219（吞 17.6 档）
    [TestCase(98.9999, 10.0, 203)]  // 旧值 197（吞 20.6 档）
    [TestCase(99.9999, 13.0, 278)]  // 旧值 274（吞 21.4 档）
    [TestCase(100.4999, 14.0, 312)] // 旧值 303（吞 22.2 档）
    public void SubTierBreakpointsMatchProberTable(double achievement, double constant, int expected)
    {
        Assert.That(SongScore.B50Ra((decimal)achievement, (decimal)constant), Is.EqualTo(expected));
    }

    // <50% 此前用系数 7.0，prober 表为 0/1.6/.../6.4，已对齐。
    [TestCase(0.0, 10.0, 0)]
    [TestCase(49.0, 10.0, 31)]  // 系数 6.4：旧值 34（用 7.0）
    public void SubFiftyMatchesProberTable(double achievement, double constant, int expected)
    {
        Assert.That(SongScore.B50Ra((decimal)achievement, (decimal)constant), Is.EqualTo(expected));
    }

    // 正常（非临界）达成率与顶档不受影响。
    [TestCase(100.5, 14.0, 315)] // 顶档 22.4，min 封顶 100.5
    [TestCase(101.0, 14.0, 315)] // 超过 100.5 仍按 100.5 算
    [TestCase(99.5, 14.0, 293)]
    [TestCase(50.0, 10.0, 40)]
    public void NormalAndTopTierUnchanged(double achievement, double constant, int expected)
    {
        Assert.That(SongScore.B50Ra((decimal)achievement, (decimal)constant), Is.EqualTo(expected));
    }
}
