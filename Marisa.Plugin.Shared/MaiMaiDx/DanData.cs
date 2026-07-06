using System.Security.Cryptography;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     段位認定（mai dan/段位表）的命令解析：版本档 + 段位名 → 前端 dan_courses.json 的查询键。
///     数据由 extract_dan_courses.py 从游戏包导出，覆盖 1.17-1.65 共 11 个版本档的固定段位
///     （段位認定 初段-十段 / 真段位認定 真初段-真十段+真皆伝+裏皆伝；随机段位不做）。
/// </summary>
public static class DanData
{
    /// <summary>缺省版本 = 国服现行对应档（1.55 PRiSM PLUS）。</summary>
    public const string DefaultVersion = "1.55";

    /// <summary>渲染缓存键的数据指纹：dan_courses.json 内容 MD5，数据更新后旧缓存自然失效。</summary>
    public static string DataHash => DataHashLazy.Value;

    private static readonly Lazy<string> DataHashLazy = new(() =>
    {
        var bytes = File.ReadAllBytes(Path.Join(ResourceManager.ResourcePath, "dan_courses.json"));
        return Convert.ToHexString(MD5.HashData(bytes));
    });

    /// <summary>版本档全集（与 dan_courses.json 的 version 字段一致）。</summary>
    private static readonly string[] Versions =
        ["1.17", "1.20", "1.25", "1.30", "1.35", "1.40", "1.45", "1.50", "1.55", "1.60", "1.65"];

    /// <summary>版本别名 → 档号。档号本身（1.55 这类）天然可输；别名大小写不敏感。</summary>
    private static readonly Dictionary<string, string> VersionAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["splash plus"]   = "1.17", ["splash+"]   = "1.17", ["splashplus"]   = "1.17", ["煌"] = "1.17", ["spl+"] = "1.17",
        ["universe"]      = "1.20", ["宙"]        = "1.20", ["uni"]          = "1.20",
        ["universe plus"] = "1.25", ["universe+"] = "1.25", ["universeplus"] = "1.25", ["星"] = "1.25", ["uni+"] = "1.25",
        ["festival"]      = "1.30", ["祭"]        = "1.30", ["fes"]          = "1.30",
        ["festival plus"] = "1.35", ["festival+"] = "1.35", ["festivalplus"] = "1.35", ["祝"] = "1.35", ["fes+"] = "1.35",
        ["buddies"]       = "1.40", ["双"]        = "1.40", ["bud"]          = "1.40",
        ["buddies plus"]  = "1.45", ["buddies+"]  = "1.45", ["buddiesplus"]  = "1.45", ["宴"] = "1.45", ["bud+"] = "1.45",
        ["prism"]         = "1.50", ["镜"]        = "1.50", ["鏡"]           = "1.50", ["pri"] = "1.50",
        ["prism plus"]    = "1.55", ["prism+"]    = "1.55", ["prismplus"]    = "1.55", ["彩"] = "1.55", ["pri+"] = "1.55",
        ["circle"]        = "1.60", ["丸"]        = "1.60", ["cir"]          = "1.60",
        ["circle plus"]   = "1.65", ["circle+"]   = "1.65", ["circleplus"]   = "1.65", ["cir+"] = "1.65",
    };

    /// <summary>版本 token 全集（档号 + 别名），长度倒序保证 longest-first（universe plus 优先于 universe）。</summary>
    private static readonly (string Token, string Version)[] VersionTokensLongestFirst =
        Versions.Select(v => (Token: v, Version: v))
            .Concat(VersionAliasMap.Select(kv => (Token: kv.Key, Version: kv.Value)))
            .OrderByDescending(t => t.Token.Length)
            .ToArray();

    /// <summary>基版本 → PLUS 档，处理「uni plus 五段」「prism +真皆传」这类拆写。</summary>
    private static readonly Dictionary<string, string> PlusUpgradeMap = new()
    {
        ["1.20"] = "1.25", ["1.30"] = "1.35", ["1.40"] = "1.45", ["1.50"] = "1.55", ["1.60"] = "1.65",
    };

    private static readonly string[] DanKanjiDigits =
        ["初", "二", "三", "四", "五", "六", "七", "八", "九", "十"];

    /// <summary>
    ///     解析「[版本] 段位名」（版本可省，空格可省）。
    ///     段位名接受简繁（皆传→皆伝、里→裏）、数字（10段→十段）与裸「皆伝」（唯一歧义消解为真皆伝）。
    /// </summary>
    public static bool TryParse(string input, out string version, out string dani, out string? error)
    {
        version = DefaultVersion;
        dani    = "";
        error   = null;

        var rest = input.Trim();
        if (rest.Length == 0)
        {
            error = "请指定段位名，如：十段、真皆伝、prism 十段、1.40 裏皆伝";
            return false;
        }

        foreach (var (token, ver) in VersionTokensLongestFirst)
        {
            if (!rest.StartsWith(token, StringComparison.OrdinalIgnoreCase)) continue;
            version = ver;
            rest    = rest[token.Length..].Trim();

            // 基版本后跟独立的 plus/+ → 升级为对应 PLUS 档
            if (rest.StartsWith('+') && PlusUpgradeMap.TryGetValue(version, out var p1))
            {
                version = p1;
                rest    = rest[1..].Trim();
            }
            else if (rest.StartsWith("plus", StringComparison.OrdinalIgnoreCase)
                     && PlusUpgradeMap.TryGetValue(version, out var p2))
            {
                version = p2;
                rest    = rest[4..].Trim();
            }
            break;
        }

        if (rest.Length == 0)
        {
            error = $"请在版本后指定段位名，如：{input.Trim()} 十段";
            return false;
        }

        if (!TryCanonicalizeDani(rest, out dani))
        {
            // 尾部本身是合法段位名时，指向版本报错（如「splash 十段」的 splash 不是已知版本）
            var lastSpace = rest.LastIndexOf(' ');
            if (lastSpace > 0 && TryCanonicalizeDani(rest[(lastSpace + 1)..], out _))
            {
                error = $"无法识别版本：{rest[..lastSpace].Trim()}（可用档号 1.17-1.65 或版本名，如 prism、彩）";
                return false;
            }
            error = $"无法识别段位名：{rest}（可用：初段-十段、真初段-真十段、真皆伝、裏皆伝）";
            return false;
        }

        // 裏皆伝 自 1.30 起设立
        if (dani == "裏皆伝" && string.CompareOrdinal(version, "1.30") < 0)
        {
            error = $"{version} 版本没有裏皆伝（1.30 起设立）";
            return false;
        }

        return true;
    }

    private static bool TryCanonicalizeDani(string raw, out string dani)
    {
        dani = "";
        // 归一：去内部空白，简繁与异体（皆传→皆伝、里→裏）
        var s = string.Concat(raw.Where(c => !char.IsWhiteSpace(c))).Replace('传', '伝').Replace('里', '裏');

        // 裸「皆伝」只有真皆伝一档（裏皆伝须显式写裏）
        if (s is "皆伝" or "真皆伝") { dani = "真皆伝"; return true; }
        if (s == "裏皆伝") { dani = "裏皆伝"; return true; }

        var shin = s.StartsWith('真');
        if (shin) s = s[1..];

        if (s.Length == 0 || s[^1] != '段') return false;
        var num = s[..^1];

        var idx = num switch
        {
            "10" => 9,
            "一"  => 0, // DanKanjiDigits[0] 是「初」
            _ when num.Length == 1 && num[0] is >= '1' and <= '9' => num[0] - '1',
            _ => Array.IndexOf(DanKanjiDigits, num),
        };
        if (idx < 0) return false;

        dani = (shin ? "真" : "") + DanKanjiDigits[idx] + "段";
        return true;
    }
}
