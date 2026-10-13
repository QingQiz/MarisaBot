namespace Marisa.Plugin.Shared.Versus;

public sealed record VersusPlayer<TScore>(
    long Qq,
    string Name,
    IReadOnlyDictionary<(long Id, int LevelIdx), TScore> Scores);

public sealed record VersusCell(int? Place, double Points, int Matchups);

public sealed record VersusRoundPlayer(long Qq, string Name, double Points, int Matchups);

public sealed record VersusStanding(string Name, double Wins, int Matchups, int Rounds, bool Left)
{
    public double? Rate => Matchups == 0 ? null : Wins / Matchups;
}

public sealed record VersusRoomSnapshot(int Round, IReadOnlyList<VersusStanding> Standings);

public sealed record VersusFetchResult<TScore>(VersusPlayer<TScore>? Player, string? Error);

public enum VersusRoomView
{
    SingleSong,
    Batch
}

public sealed record VersusRoomPresentation(string Version, VersusRoomView View);

/// <param name="PickCount">为空时比较全部谱面并关房；否则每轮随机抽取。</param>
public sealed record VersusRoomPlan<TChart>(string Title, IReadOnlyList<TChart> Charts, int? PickCount)
{
    public bool Continuous => PickCount is not null;
}
