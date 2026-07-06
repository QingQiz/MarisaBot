using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxDanDataTest
{
    // 段位名（无版本 → 缺省 1.55）：简繁/数字/裸皆伝 归一
    [TestCase("十段",     "十段")]
    [TestCase("10段",     "十段")]
    [TestCase("初段",     "初段")]
    [TestCase("1段",      "初段")]
    [TestCase("一段",     "初段")]
    [TestCase("三段",     "三段")]
    [TestCase("真十段",   "真十段")]
    [TestCase("真10段",   "真十段")]
    [TestCase("真1段",    "真初段")]
    [TestCase("真皆伝",   "真皆伝")]
    [TestCase("真皆传",   "真皆伝")]
    [TestCase("皆传",     "真皆伝")]
    [TestCase("裏皆伝",   "裏皆伝")]
    [TestCase("里皆传",   "裏皆伝")]
    [TestCase("真 十段",  "真十段")] // 段位名内部空格容忍
    [TestCase("10 段",    "十段")]
    public void ParsesDaniWithDefaultVersion(string raw, string dani)
    {
        Assert.That(DanData.TryParse(raw, out var v, out var d, out _), Is.True);
        Assert.That(v, Is.EqualTo(DanData.DefaultVersion));
        Assert.That(d, Is.EqualTo(dani));
    }

    // 版本：档号 / 别名（大小写不敏感）/ 代字，空格可省，longest-first
    [TestCase("1.50 十段",           "1.50", "十段")]
    [TestCase("1.50十段",            "1.50", "十段")]
    [TestCase("prism 十段",          "1.50", "十段")]
    [TestCase("PRISM 十段",          "1.50", "十段")]
    [TestCase("彩十段",              "1.55", "十段")]
    [TestCase("prism+真皆传",        "1.55", "真皆伝")]
    [TestCase("universe 五段",       "1.20", "五段")]
    [TestCase("universe plus 五段",  "1.25", "五段")]
    [TestCase("uni+五段",            "1.25", "五段")]
    [TestCase("星五段",              "1.25", "五段")]
    [TestCase("cir+ 里皆传",         "1.65", "裏皆伝")]
    [TestCase("splash+ 八段",        "1.17", "八段")]
    [TestCase("uni plus 五段",       "1.25", "五段")]  // 基版本 + 独立 plus/+ 拆写升级
    [TestCase("fes plus 十段",       "1.35", "十段")]
    [TestCase("prism +真皆传",       "1.55", "真皆伝")]
    [TestCase("1.30 裏皆伝",         "1.30", "裏皆伝")] // 裏皆伝设立边界（1.17/1.25 在拒绝组）
    public void ParsesVersionToken(string raw, string version, string dani)
    {
        Assert.That(DanData.TryParse(raw, out var v, out var d, out _), Is.True);
        Assert.That(v, Is.EqualTo(version));
        Assert.That(d, Is.EqualTo(dani));
    }

    // 拒绝：空输入 / 只有版本 / 未知段位 / 裏皆伝早于 1.30
    [TestCase("")]
    [TestCase("彩")]
    [TestCase("prism plus")]
    [TestCase("十一段")]
    [TestCase("xyz")]
    [TestCase("1.17 裏皆伝")]
    [TestCase("1.25 里皆传")]
    public void RejectsInvalidInput(string raw)
    {
        Assert.That(DanData.TryParse(raw, out _, out _, out var error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    // 前缀不是已知版本而尾部是合法段位名时，错误指向版本而非段位名
    [TestCase("splash 十段")]
    [TestCase("1.15 真皆伝")]
    public void UnknownVersionYieldsVersionError(string raw)
    {
        Assert.That(DanData.TryParse(raw, out _, out _, out var error), Is.False);
        Assert.That(error, Does.StartWith("无法识别版本"));
    }
}
