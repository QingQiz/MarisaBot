namespace Marisa.Plugin.Shared.Chunithm;

/// <summary>数组索引与游戏难度分开保存，避免缺少 ULTIMA 时把 WORLD'S END 当成 ULTIMA。</summary>
public sealed record ChunithmVersusChart(ChunithmSong Song, int ChartIndex, int LevelIndex)
{
    public long Id => Song.Id;
    public double Constant => Song.Constants[ChartIndex];
    public string Level => Song.Levels[ChartIndex];
    public string LevelName => ChunithmSong.LevelLabel[LevelIndex];
}

public static class ChunithmVersusCatalog
{
    public static IReadOnlyList<ChunithmVersusChart> StandardCharts(IEnumerable<ChunithmSong> songs) =>
        songs.Where(song => !DataFetcher.DataFetcher.IsDeletedSong(song.Id))
            .SelectMany(song => Enumerable.Range(0, song.DiffNames.Count)
                .Where(i => i < song.Constants.Count && i < song.Levels.Count)
                .Select(i => (ChartIndex: i, LevelIndex: DifficultyIndex(song.DiffNames[i])))
                .Where(x => x.LevelIndex is >= 0 and <= 4)
                .Select(x => new ChunithmVersusChart(song, x.ChartIndex, x.LevelIndex)))
            .ToArray();

    public static ChunithmVersusChart? FindChart(ChunithmSong song, int levelIndex) =>
        StandardCharts([song]).FirstOrDefault(chart => chart.LevelIndex == levelIndex);

    public static int DifficultyIndex(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BASIC" or "BAS" or "BSC" => 0,
        "ADVANCED" or "ADV" => 1,
        "EXPERT" or "EXP" => 2,
        "MASTER" or "MAS" or "MST" => 3,
        "ULTIMA" or "ULT" => 4,
        "WORLD'S END" or "WORLDS END" or "WORLD’S END" or "WE" => 5,
        _ => -1
    };

    /// <summary>只归一化对战标准谱；不改变旧汇总/查分路径。未知、歧义标题和 WE 不参与比较。</summary>
    public static Dictionary<(long Id, int LevelIdx), ChunithmScore> NormalizeScores(
        IEnumerable<ChunithmScore> records, IReadOnlyList<ChunithmSong> catalog)
    {
        var byId = catalog.ToDictionary(song => song.Id);
        var byTitle = catalog.GroupBy(song => song.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        var charts = StandardCharts(catalog).ToDictionary(chart => (chart.Id, chart.LevelIndex));
        var result = new Dictionary<(long Id, int LevelIdx), ChunithmScore>();

        foreach (var record in records)
        {
            var difficulty = ScoreDifficulty(record);
            if (difficulty is < 0 or > 4) continue;

            byId.TryGetValue(record.Id, out var song);
            if (song is null || (!string.IsNullOrWhiteSpace(record.Title) &&
                                 !song.Title.Equals(record.Title, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(record.Title) || !byTitle.TryGetValue(record.Title, out song)) continue;
            }

            var key = (song.Id, difficulty);
            if (!charts.TryGetValue(key, out var chart)) continue;
            var normalized = new ChunithmScore
            {
                Id = song.Id,
                Title = song.Title,
                LevelIndex = difficulty,
                LevelLabel = chart.LevelName,
                Level = chart.Level,
                Constant = (decimal)chart.Constant,
                Achievement = record.Achievement,
                Fc = record.FullCombo,
                FullChain = record.FullChain,
                Clear = record.Clear
            };
            // 分数与最佳通关/COMBO 标记可来自不同游玩；同分或较低分不能抹掉已知标记。
            result[key] = result.TryGetValue(key, out var previous)
                ? ChunithmBattleCompletion.Merge(previous, normalized)
                : normalized;
        }

        return result;
    }

    private static int ScoreDifficulty(ChunithmScore record)
    {
        var hasLabel = !string.IsNullOrWhiteSpace(record.LevelLabel);
        var labelIndex = DifficultyIndex(record.LevelLabel);
        if (!record.HasLevelIndex && !hasLabel)
            throw new InvalidDataException("完整成绩响应缺少明确的谱面难度，无法进行对战");
        if (hasLabel && labelIndex < 0)
            throw new InvalidDataException("完整成绩响应包含未知难度标签，无法进行对战");
        if (!record.HasLevelIndex) return labelIndex;
        if (record.LevelIndex is < 0 or > 5)
            throw new InvalidDataException("完整成绩响应包含无效难度索引，无法进行对战");
        if (hasLabel && record.LevelIndex != labelIndex)
            throw new InvalidDataException("完整成绩响应的难度索引与标签不一致，无法进行对战");
        return (int)record.LevelIndex;
    }
}
