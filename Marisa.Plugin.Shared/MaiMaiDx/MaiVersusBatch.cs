namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>批量对战的内存模型和前端分页投影。</summary>
public sealed class MaiVersusBatch
{
    public const int DefaultPageSize = 20;

    private readonly IReadOnlyList<Row> _rows;

    public MaiVersusBatch(
        string scope,
        string version,
        string sortLabel,
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        Player left,
        Player right,
        int pageSize = DefaultPageSize)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        Scope = scope;
        Version = version;
        SortLabel = sortLabel;
        PageSize = pageSize;
        Players = [left, right];
        _rows = charts.Select(chart => CreateRow(chart, left, right)).ToArray();

        Summary = new BatchSummary(
            _rows.Count(x => x.Outcome == Outcome.Left),
            _rows.Count(x => x.Outcome == Outcome.Right),
            _rows.Count(x => x.Outcome == Outcome.Draw),
            _rows.Count(x => x.Outcome == Outcome.Unplayed));
    }

    public string Scope { get; }
    public string Version { get; }
    public string SortLabel { get; }
    public int PageSize { get; }
    public IReadOnlyList<Player> Players { get; }
    public BatchSummary Summary { get; }
    public int TotalCharts => _rows.Count;
    public int PageCount => Math.Max(1, (TotalCharts + PageSize - 1) / PageSize);

    public PageData GetPage(int page)
    {
        if (page < 1 || page > PageCount) throw new ArgumentOutOfRangeException(nameof(page));

        return new PageData(
            Scope,
            Version,
            SortLabel,
            Players.Select(x => new PlayerView(x.Name)).ToArray(),
            page,
            PageSize,
            TotalCharts,
            Summary,
            _rows.Skip((page - 1) * PageSize).Take(PageSize).ToArray());
    }

    private static Row CreateRow(
        (double Constant, int LevelIdx, MaiMaiSong Song) chart,
        Player left,
        Player right)
    {
        var key = (chart.Song.Id, chart.LevelIdx);
        var leftScore = CreateScore(left, key, chart.Song, chart.LevelIdx);
        var rightScore = CreateScore(right, key, chart.Song, chart.LevelIdx);
        var outcome = Compare(leftScore, rightScore);

        return new Row(
            chart.Song.Id,
            chart.Song.Title,
            chart.Song.Type,
            chart.LevelIdx,
            chart.Song.Levels[chart.LevelIdx],
            chart.Constant,
            chart.Song.Charts[chart.LevelIdx].Notes.Sum() * 3,
            leftScore,
            rightScore,
            outcome);
    }

    private static ScoreView CreateScore(
        Player player,
        (long Id, int LevelIdx) key,
        MaiMaiSong song,
        int levelIdx)
    {
        if (!player.Scores.TryGetValue(key, out var score))
        {
            return new ScoreView(State.Unplayed);
        }

        return new ScoreView(
            State.Played,
            score.Achievement,
            SongScore.CalcRank(score.Achievement),
            SongScore.Ra(score.Achievement, song.Constants[levelIdx]),
            score.DxScore,
            score.Fc ?? string.Empty,
            score.Fs ?? string.Empty);
    }

    private static string Compare(ScoreView left, ScoreView right)
    {
        if (left.State == State.Unplayed && right.State == State.Unplayed) return Outcome.Unplayed;
        if (left.State == State.Unplayed) return Outcome.Right;
        if (right.State == State.Unplayed) return Outcome.Left;
        if (left.Achievement == right.Achievement) return Outcome.Draw;
        return left.Achievement > right.Achievement ? Outcome.Left : Outcome.Right;
    }

    public sealed record Player(
        string Name,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> Scores);

    public sealed record BatchSummary(int LeftWins, int RightWins, int Draws, int Unplayed);

    public sealed record PlayerView(string Name);

    public sealed record ScoreView(
        string State,
        double? Achievement = null,
        string? Rank = null,
        int? Rating = null,
        int? DxScore = null,
        string Fc = "",
        string Fs = "");

    public sealed record Row(
        long Id,
        string Title,
        string Type,
        int LevelIndex,
        string Level,
        double Constant,
        long MaxDx,
        ScoreView Left,
        ScoreView Right,
        string Outcome);

    public sealed record PageData(
        string Scope,
        string Version,
        string SortLabel,
        IReadOnlyList<PlayerView> Players,
        int Page,
        int PageSize,
        int TotalCharts,
        BatchSummary Summary,
        IReadOnlyList<Row> Rows);

    private static class State
    {
        public const string Played = "played";
        public const string Unplayed = "unplayed";
    }

    private static class Outcome
    {
        public const string Left = "left";
        public const string Right = "right";
        public const string Draw = "draw";
        public const string Unplayed = "unplayed";
    }
}
