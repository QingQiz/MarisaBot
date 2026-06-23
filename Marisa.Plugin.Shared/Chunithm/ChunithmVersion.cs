namespace Marisa.Plugin.Shared.Chunithm;

/// <summary>
///     国服中二节奏版本的有序编号（来自 lxns 权威版本表，DX 时代从 20000 起）。
///     版本名是带空格的字符串，故用「名字 → 编号」表而非 enum；用编号比较替代散落各 fetcher 的
///     硬编码「最新版本名集合」和按版本名字典序排序取最新的脆弱写法。
/// </summary>
public static class ChunithmVersion
{
    public static readonly IReadOnlyDictionary<string, int> NameToCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["CHUNITHM"]               = 10000,
        ["CHUNITHM PLUS"]          = 10500,
        ["CHUNITHM AIR"]           = 11000,
        ["CHUNITHM AIR PLUS"]      = 11500,
        ["CHUNITHM STAR"]          = 12000,
        ["CHUNITHM STAR PLUS"]     = 12500,
        ["CHUNITHM AMAZON"]        = 13000,
        ["CHUNITHM AMAZON PLUS"]   = 13500,
        ["CHUNITHM CRYSTAL"]       = 14000,
        ["CHUNITHM CRYSTAL PLUS"]  = 14500,
        ["CHUNITHM PARADISE"]      = 15000,
        ["CHUNITHM PARADISE LOST"] = 15500,
        ["CHUNITHM NEW"]           = 20000,
        ["CHUNITHM NEW PLUS"]      = 20500,
        ["CHUNITHM SUN"]           = 21000,
        ["CHUNITHM SUN PLUS"]      = 21500,
        ["CHUNITHM LUMINOUS"]      = 22000,
        ["CHUNITHM LUMINOUS PLUS"] = 22500,
        ["CHUNITHM VERSE"]         = 23000,
    };

    /// <summary>
    ///     「当前版本」下限：该版本及更新的算「新版本」（b30/recent 拆分用）。
    ///     国服当前版本（中二节奏 2026）涵盖国际版 LUMINOUS PLUS + VERSE 两版，故取 LUMINOUS PLUS。
    /// </summary>
    public const int CurrentVersionCode = 22500;

    public static int CodeOf(string? versionName) =>
        versionName != null && NameToCode.TryGetValue(versionName, out var code) ? code : 0;

    /// <summary>版本名是否属于「当前版本」（用于把成绩分到 recent 而非 best 池）。</summary>
    public static bool IsCurrent(string? versionName) => CodeOf(versionName) >= CurrentVersionCode;
}
