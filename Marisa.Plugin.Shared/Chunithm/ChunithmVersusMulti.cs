using Marisa.Plugin.Shared.Versus;

namespace Marisa.Plugin.Shared.Chunithm;

/// <summary>中二游戏投影；对位规则、抽样及累计榜使用共享对战核心。</summary>
public sealed class ChunithmVersusMulti
{
    public ChunithmVersusMulti(string title, string version, IReadOnlyList<ChunithmVersusChart> charts,
        IReadOnlyList<VersusPlayer<ChunithmScore>> players, int pageSize = ChunithmVersusBatch.DefaultPageSize)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        Title = title;
        Version = version;
        Players = players;
        PageSize = pageSize;
        Rows = charts.Select(chart => CreateRow(chart, players)).ToArray();
        Totals = players.Select((_, index) => new Total(Rows.Sum(row => row.Cells[index].Points), Rows.Sum(row => row.Cells[index].Matchups))).ToArray();
    }

    public string Title { get; }
    public string Version { get; }
    public IReadOnlyList<VersusPlayer<ChunithmScore>> Players { get; }
    public IReadOnlyList<Row> Rows { get; }
    public IReadOnlyList<Total> Totals { get; }
    public int PageSize { get; }
    public int PageCount => Math.Max(1, (Rows.Count + PageSize - 1) / PageSize);

    public static List<ChunithmVersusChart> PickCharts(IEnumerable<ChunithmVersusChart> charts,
        IReadOnlyList<IReadOnlyDictionary<(long Id, int LevelIdx), ChunithmScore>> scores, int count, Random random) =>
        VersusSampler.Pick(charts, scores, chart => chart.ScoreKey, chart => chart.Constant, count, random);

    public SingleData GetSingle(int round, IReadOnlyList<VersusStanding> standings)
    {
        var row = Rows.Single();
        var order = Order(index => row.Cells[index].Place ?? int.MaxValue);
        var lowest = row.Cells.Max(cell => cell.Place ?? 0);
        return new SingleData(row with { Cells = [] }, round,
            order.Select(index => new SinglePlayer(Players[index].Name, row.Cells[index].Place, row.Cells[index].Score,
                lowest > 1 && row.Cells[index].Place == lowest)).ToArray(), standings);
    }

    public PageData GetPage(int page, int round, IReadOnlyList<VersusStanding> standings)
    {
        if (page < 1 || page > PageCount) throw new ArgumentOutOfRangeException(nameof(page));
        var order = Order(PlaceOf);
        return new PageData(Title, Version, round,
            order.Select(index => new PlayerView(Players[index].Name, PlaceOf(index), Totals[index].Points, Totals[index].Matchups)).ToArray(),
            page, PageSize, Rows.Count, Rows.Skip((page - 1) * PageSize).Take(PageSize)
                .Select(row => row with { Cells = order.Select(index => row.Cells[index]).ToArray() }).ToArray(), standings);
    }

    public IReadOnlyList<VersusRoundPlayer> ForRoom() => Players.Select((player, index) =>
        new VersusRoundPlayer(player.Qq, player.Name, Totals[index].Points, Totals[index].Matchups)).ToArray();

    private int PlaceOf(int index) => 1 + Totals.Count(total => total.Points > Totals[index].Points);
    private int[] Order(Func<int, int> place) => Enumerable.Range(0, Players.Count).OrderBy(place).ThenBy(index => index).ToArray();

    private static Row CreateRow(ChunithmVersusChart chart, IReadOnlyList<VersusPlayer<ChunithmScore>> players)
    {
        var scores = players.Select(player => ChunithmVersusBatch.CreateScore(player.Scores, chart)).ToArray();
        var rules = VersusRules.ScoreChart(scores.Select(score => (double?)score.Score).ToArray());
        return new Row(new ChunithmVersusBatch.SongView(chart), chart.LevelIndex, chart.LevelName, chart.ChartIndex,
            chart.Level, chart.Constant, scores.Select((score, index) =>
                new Cell(score, rules[index].Place, rules[index].Points, rules[index].Matchups)).ToArray());
    }

    public sealed record Total(double Points, int Matchups);
    public sealed record Cell(ChunithmVersusBatch.ScoreView Score, int? Place, double Points, int Matchups);
    public sealed record Row(ChunithmVersusBatch.SongView Song, int LevelIndex, string LevelName, int ChartIndex,
        string Level, double Constant, IReadOnlyList<Cell> Cells);
    public sealed record SinglePlayer(string Name, int? Place, ChunithmVersusBatch.ScoreView Score, bool Stamp);
    public sealed record SingleData(Row Chart, int Round, IReadOnlyList<SinglePlayer> Players, IReadOnlyList<VersusStanding> Standings);
    public sealed record PlayerView(string Name, int Place, double Points, int Matchups);
    public sealed record PageData(string Title, string Version, int Round, IReadOnlyList<PlayerView> Players,
        int Page, int PageSize, int TotalCharts, IReadOnlyList<Row> Rows, IReadOnlyList<VersusStanding> Standings);
}
