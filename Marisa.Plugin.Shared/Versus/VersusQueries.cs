using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Versus;

public sealed record VersusScopeSelection<TChart>(IReadOnlyList<TChart> Charts, string SortLabel);

/// <summary>共享对战查询顺序与数量校验，难度词表和范围语法由游戏提供。</summary>
public static class VersusQueries
{
    public static (List<TSong> Songs, int LevelIndex, bool Random) ResolveSongQuery<TSong>(
        SongDb<TSong> songs,
        string input,
        Func<string, int?> difficulty,
        Func<ReadOnlyMemory<char>, (int Level, ReadOnlyMemory<char> Remaining)?> stripAffix,
        IReadOnlyList<TSong>? additionalSongs = null)
        where TSong : Song
    {
        var query = input.Trim();
        if (query.Length == 0) return ([], 3, true);

        var exact = Search(query.AsMemory(), true);
        if (exact.Count > 0) return (exact, 3, false);
        if (difficulty(query) is { } levelIndex) return ([], levelIndex, true);

        var affix = stripAffix(query.AsMemory());
        if (affix is { } parsed)
        {
            exact = Search(parsed.Remaining, true);
            if (exact.Count > 0) return (exact, parsed.Level, false);
        }

        var fuzzy = Search(query.AsMemory(), false);
        if (fuzzy.Count > 0) return (fuzzy, 3, false);
        return affix is { } fallback
            ? (Search(fallback.Remaining, false), fallback.Level, false)
            : ([], 3, false);

        List<TSong> Search(ReadOnlyMemory<char> input, bool exactMatch)
        {
            var local = exactMatch ? songs.SearchSongExact(input) : songs.SearchSong(input);
            if (additionalSongs is null) return local;

            var value = input.Span.Trim();
            if (exactMatch && value.IsEmpty) return local;
            var title = value.ToString();
            IEnumerable<TSong> additional;
            if (!exactMatch)
            {
                additional = additionalSongs.Where(song => song.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
            }
            else if (value.StartsWith("id", StringComparison.OrdinalIgnoreCase) &&
                     long.TryParse(value[2..].Trim(), out var explicitId))
            {
                additional = additionalSongs.Where(song => song.Id == explicitId);
            }
            else if (long.TryParse(value, out var id) && additionalSongs.Any(song => song.Id == id))
            {
                additional = additionalSongs.Where(song => song.Id == id);
            }
            else
            {
                additional = additionalSongs.Where(song => song.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            }

            return local.Concat(additional).DistinctBy(song => song.Id).ToList();
        }
    }

    /// <summary>解析 `N [难度/范围]`；数量或查询无效时已回复原因。</summary>
    public static VersusRandomSelection<TChart>? ParseRandom<TChart>(
        Message message,
        string usage,
        int maxCount,
        Func<string, int?> difficulty,
        Func<int, IReadOnlyList<TChart>> levelCharts,
        Func<string, (VersusScopeSelection<TChart>? Scope, string? Error)> parseScope,
        Func<string, string> invalidQuery)
    {
        var command = message.Command.Trim().ToString();
        var digits = command.TakeWhile(char.IsAsciiDigit).Count();
        if (!int.TryParse(command[..digits], out var count) || count < 1 || count > maxCount)
        {
            message.Reply($"N 须为 1～{maxCount} 的整数，用法：{usage}");
            return null;
        }

        var query = command[digits..].Trim();
        if (query.Length == 0) return new(count, query, levelCharts(3));
        if (difficulty(query) is { } levelIndex) return new(count, query, levelCharts(levelIndex));
        if (parseScope(query).Scope is { } scope) return new(count, query, scope.Charts);

        message.Reply(invalidQuery(query));
        return null;
    }

    /// <summary>解析批量范围，并在获取玩家成绩前拒绝没有谱面的范围。</summary>
    public static VersusBatchSelection<TChart>? ParseBatch<TChart>(
        Message message,
        Func<string, (VersusScopeSelection<TChart>? Scope, string? Error)> parseScope)
    {
        var query = message.Command.Trim().ToString();
        var (scope, error) = parseScope(query);
        if (scope is null)
        {
            message.Reply(error!);
            return null;
        }
        if (scope.Charts.Count == 0)
        {
            message.Reply($"没有找到 {query} 对应的谱面");
            return null;
        }

        return new(query, scope.SortLabel, scope.Charts);
    }
}
