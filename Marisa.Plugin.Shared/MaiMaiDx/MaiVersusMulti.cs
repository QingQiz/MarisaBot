namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     多人对战一轮的结果。同一谱面上任意两人算一次对位：达成率高者胜，平局各得 0.5，
///     没玩过的输给玩过的，两人都没玩过不算对位。
/// </summary>
public sealed class MaiVersusMulti
{
    public MaiVersusMulti(
        string title,
        string version,
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        IReadOnlyList<Player> players,
        int pageSize = MaiVersusBatch.DefaultPageSize)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        Title = title;
        Version = version;
        Players = players;
        PageSize = pageSize;
        Rows = charts.Select(chart => CreateRow(chart, players)).ToArray();
        Totals = players.Select((_, i) => new Total(
            Rows.Sum(row => row.Cells[i].Points),
            Rows.Sum(row => row.Cells[i].Matchups))).ToArray();
    }

    public string Title { get; }
    public string Version { get; }
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<Row> Rows { get; }
    public IReadOnlyList<Total> Totals { get; }
    public int PageSize { get; }
    public int PageCount => Math.Max(1, (Rows.Count + PageSize - 1) / PageSize);

    /// <summary>
    ///     从至少两人玩过的谱面里抽 <paramref name="count" /> 首：先找能凑够候选池的最高覆盖人数门槛，
    ///     再在池内加权不放回抽取，每多一人玩过权重翻倍。全员都玩过的谱面太少时宁可降低门槛，也要保留随机性。
    /// </summary>
    public static List<(double Constant, int LevelIdx, MaiMaiSong Song)> PickCharts(
        IEnumerable<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        IReadOnlyList<IReadOnlyDictionary<(long Id, int LevelIdx), SongScore>> scores,
        int count,
        Random random)
    {
        var candidates = charts
            .Select(chart => (Chart: chart, Played: scores.Count(s => s.ContainsKey((chart.Song.Id, chart.LevelIdx)))))
            .Where(x => x.Played >= 2)
            .ToList();

        var poolSize  = Math.Max(10, count * 2);
        var threshold = Enumerable.Range(2, Math.Max(0, scores.Count - 1)).Reverse()
            .FirstOrDefault(t => candidates.Count(x => x.Played >= t) >= poolSize, 2);

        return candidates
            .Where(x => x.Played >= threshold)
            .Select(x => (x.Chart, Key: Math.Log(1 - random.NextDouble()) / Math.Pow(2, x.Played - threshold)))
            .OrderByDescending(x => x.Key)
            .Take(count)
            .Select(x => x.Chart)
            .OrderByDescending(x => x.Constant)
            .ThenBy(x => x.Song.Id)
            .ToList();
    }

    /// <summary>单曲排行：“菜”章只盖给玩过的人里达成率最低的，未游玩不盖。</summary>
    public SingleData GetSingle(int round, IReadOnlyList<Standing> standings)
    {
        var row    = Rows.Single();
        var ranked = Order(i => row.Cells[i].Place ?? int.MaxValue);
        var lowest = row.Cells.Max(c => c.Place ?? 0);

        return new SingleData(
            row with { Cells = [] },
            round,
            ranked.Select(i => new SinglePlayer(
                Players[i].Name,
                row.Cells[i].Place,
                row.Cells[i].Score,
                lowest > 1 && row.Cells[i].Place == lowest)).ToArray(),
            standings);
    }

    public PageData GetPage(int page, int round, IReadOnlyList<Standing> standings)
    {
        if (page < 1 || page > PageCount) throw new ArgumentOutOfRangeException(nameof(page));

        var order = Order(PlaceOf);

        return new PageData(
            Title,
            Version,
            round,
            order.Select(i => new PlayerView(Players[i].Name, PlaceOf(i), Totals[i].Points, Totals[i].Matchups)).ToArray(),
            page,
            PageSize,
            Rows.Count,
            Rows.Skip((page - 1) * PageSize).Take(PageSize)
                .Select(row => row with { Cells = order.Select(i => row.Cells[i]).ToArray() })
                .ToArray(),
            standings);
    }

    private int PlaceOf(int index) => 1 + Totals.Count(t => t.Points > Totals[index].Points);

    private int[] Order(Func<int, int> place) =>
        Enumerable.Range(0, Players.Count).OrderBy(place).ThenBy(i => i).ToArray();

    private static Row CreateRow((double Constant, int LevelIdx, MaiMaiSong Song) chart, IReadOnlyList<Player> players)
    {
        var scores       = players.Select(p => MaiVersusBatch.CreateScore(p.Scores, chart.Song, chart.LevelIdx)).ToArray();
        var achievements = scores.Select(s => s.Achievement).ToArray();
        var played       = achievements.Count(a => a is not null);

        return new Row(
            new SongView(chart.Song),
            chart.LevelIdx,
            chart.Song.Levels[chart.LevelIdx],
            chart.Constant,
            chart.Song.Charts[chart.LevelIdx].Notes.Sum() * 3,
            scores.Select((score, i) => CreateCell(score, i)).ToArray());

        Cell CreateCell(MaiVersusBatch.ScoreView score, int index)
        {
            if (achievements[index] is not { } achievement) return new Cell(score, null, 0, played);

            var others = achievements.Where((_, j) => j != index).ToArray();
            var points = others.Sum(other => other is not { } a || a < achievement ? 1 : a == achievement ? 0.5 : 0);

            return new Cell(score, 1 + others.Count(a => a > achievement), points, others.Length);
        }
    }

    public sealed record Player(
        long Qq,
        string Name,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> Scores);

    public sealed record Total(double Points, int Matchups);

    public sealed record Cell(MaiVersusBatch.ScoreView Score, int? Place, double Points, int Matchups);

    public sealed record SongView(long Id, string Title, string Type, string Artist, string Genre, long Bpm, string From, bool IsNew)
    {
        public SongView(MaiMaiSong song)
            : this(song.Id, song.Title, song.Type, song.Info.Artist, song.Info.Genre, song.Info.Bpm, song.Info.From, song.Info.IsNew)
        {
        }
    }

    public sealed record Row(
        SongView Song,
        int LevelIndex,
        string Level,
        double Constant,
        long MaxDx,
        IReadOnlyList<Cell> Cells);

    public sealed record SinglePlayer(string Name, int? Place, MaiVersusBatch.ScoreView Score, bool Stamp);

    public sealed record SingleData(
        Row Chart,
        int Round,
        IReadOnlyList<SinglePlayer> Players,
        IReadOnlyList<Standing> Standings);

    public sealed record PlayerView(string Name, int Place, double Points, int Matchups);

    public sealed record PageData(
        string Title,
        string Version,
        int Round,
        IReadOnlyList<PlayerView> Players,
        int Page,
        int PageSize,
        int TotalCharts,
        IReadOnlyList<Row> Rows,
        IReadOnlyList<Standing> Standings);

    public sealed record Standing(string Name, double Wins, int Matchups, int Rounds, bool Left)
    {
        public double? Rate => Matchups == 0 ? null : Wins / Matchups;
    }
}
