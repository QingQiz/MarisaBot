using Marisa.Plugin.Shared.Versus;

namespace Marisa.Plugin.Shared.Chunithm;

public sealed class ChunithmVersusBatch
{
    public const int DefaultPageSize = 20;
    private readonly IReadOnlyList<Row> _rows;

    public ChunithmVersusBatch(string scope, string version, string sortLabel,
        IReadOnlyList<ChunithmVersusChart> charts,
        VersusPlayer<ChunithmScore> left, VersusPlayer<ChunithmScore> right,
        int pageSize = DefaultPageSize)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        Scope = scope;
        Version = version;
        SortLabel = sortLabel;
        PageSize = pageSize;
        Players = [left, right];
        _rows = charts.Select(chart => CreateRow(chart, left, right)).ToArray();
        Summary = new BatchSummary(_rows.Count(row => row.Outcome == "left"), _rows.Count(row => row.Outcome == "right"),
            _rows.Count(row => row.Outcome == "draw"), _rows.Count(row => row.Outcome == "unplayed"));
    }

    public string Scope { get; }
    public string Version { get; }
    public string SortLabel { get; }
    public int PageSize { get; }
    public IReadOnlyList<VersusPlayer<ChunithmScore>> Players { get; }
    public BatchSummary Summary { get; }
    public int TotalCharts => _rows.Count;
    public int PageCount => Math.Max(1, (TotalCharts + PageSize - 1) / PageSize);

    public PageData GetPage(int page)
    {
        if (page < 1 || page > PageCount) throw new ArgumentOutOfRangeException(nameof(page));
        return new PageData(Scope, Version, SortLabel, Players.Select(player => new PlayerView(player.Name)).ToArray(),
            page, PageSize, TotalCharts, Summary, _rows.Skip((page - 1) * PageSize).Take(PageSize).ToArray());
    }

    public static SingleData GetSingle(ChunithmVersusChart chart, VersusPlayer<ChunithmScore> left, VersusPlayer<ChunithmScore> right)
    {
        var row = CreateRow(chart, left, right);
        var winner = row.Outcome switch { "left" => 0, "right" => 1, _ => -1 };
        return new SingleData(new SongView(chart), chart.LevelIndex, chart.LevelName, chart.ChartIndex, chart.Level, chart.Constant,
            [new SinglePlayer(left.Name, row.Left.State == "played", row.Left), new SinglePlayer(right.Name, row.Right.State == "played", row.Right)], winner);
    }

    public static ScoreView CreateScore(IReadOnlyDictionary<(long Id, int LevelIdx), ChunithmScore> scores, ChunithmVersusChart chart) =>
        scores.TryGetValue((chart.Id, chart.LevelIndex), out var score)
            ? new ScoreView("played", score.Achievement, ChunithmScore.GetRank(score.Achievement),
                ChunithmSong.Ra(score.Achievement, (decimal)chart.Constant), score.FullCombo, score.FullChain, score.Clear ?? "")
            : new ScoreView("unplayed");

    private static Row CreateRow(ChunithmVersusChart chart, VersusPlayer<ChunithmScore> left, VersusPlayer<ChunithmScore> right)
    {
        var a = CreateScore(left.Scores, chart);
        var b = CreateScore(right.Scores, chart);
        return new Row(chart.Id, chart.Song.Title, chart.LevelIndex, chart.LevelName, chart.ChartIndex,
            chart.Level, chart.Constant, a, b, VersusRules.Compare(a.Score, b.Score));
    }

    public sealed record BatchSummary(int LeftWins, int RightWins, int Draws, int Unplayed);
    public sealed record PlayerView(string Name);
    public sealed record ScoreView(string State, int? Score = null, string? Rank = null, decimal? Rating = null,
        string FullCombo = "", string FullChain = "", string Clear = "");
    public sealed record SongView(long Id, string Title, string Artist, string Genre, string Version, string Bpm)
    {
        public SongView(ChunithmVersusChart chart)
            : this(chart.Id, chart.Song.Title, chart.Song.Artist, chart.Song.Genre, chart.Song.Version,
                chart.ChartIndex < chart.Song.BpmList.Count ? string.Join(" / ", chart.Song.BpmList[chart.ChartIndex]) : "") { }
    }
    public sealed record Row(long Id, string Title, int LevelIndex, string LevelName, int ChartIndex, string Level,
        double Constant, ScoreView Left, ScoreView Right, string Outcome);
    public sealed record PageData(string Scope, string Version, string SortLabel, IReadOnlyList<PlayerView> Players,
        int Page, int PageSize, int TotalCharts, BatchSummary Summary, IReadOnlyList<Row> Rows);
    public sealed record SinglePlayer(string Name, bool Played, ScoreView Score);
    public sealed record SingleData(SongView Song, int LevelIndex, string LevelName, int ChartIndex, string Level,
        double Constant, IReadOnlyList<SinglePlayer> Players, int WinnerIndex);
}
