using System.Text.RegularExpressions;

namespace Marisa.Plugin.Shared.Osu;

public partial class OsuCommandParser
{
    public record BpRank(int Start, int? End);

    public record OsuCommand(string Name, BpRank? Rank, int? Mode);

    public static OsuCommand? Parse(string input)
    {
        input = input.Trim();

        if (input.Length == 0) return new OsuCommand("", null, null);

        if (int.TryParse(input, out var n) && n > 0 && n <= 200)
            return new OsuCommand("", new BpRank(n, null), null);

        var rankMatch = RankRegex().Match(input);
        BpRank? rank = null;
        if (rankMatch.Success)
        {
            var a = int.Parse(rankMatch.Groups[1].Value);
            var b = rankMatch.Groups[2].Success ? int.Parse(rankMatch.Groups[2].Value) : (int?)null;
            if (a >= 1 && a <= 200 && (!b.HasValue || b is >= 1 and <= 200) && (b == null || a < b))
                rank = new BpRank(a, b);
        }

        var modeMatch = ModeRegex().Match(input);
        int? mode = modeMatch.Success ? ParseMode(modeMatch.Groups[1].Value) : null;

        var hashIdx = input.IndexOf('#');
        var colonIdx = input.IndexOfAny([':', '\uff1a']);
        var splitIdx = hashIdx >= 0 ? hashIdx : colonIdx >= 0 ? colonIdx : input.Length;

        return new OsuCommand(input[..splitIdx].Trim(), rank, mode);
    }

    private static int? ParseMode(string s) => s switch
    {
        "osu" => 0,
        "taiko" => 1,
        "fruit" => 2,
        "mania" => 3,
        _ when int.TryParse(s, out var m) && m is >= 0 and <= 3 => m,
        _ => null
    };

    [GeneratedRegex(@"#(\d+)(?:-(\d+))?")]
    private static partial Regex RankRegex();

    [GeneratedRegex(@"[:：](\d+|osu|taiko|fruit|mania)$")]
    private static partial Regex ModeRegex();
}
