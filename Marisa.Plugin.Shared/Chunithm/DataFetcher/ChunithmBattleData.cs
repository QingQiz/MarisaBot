namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public sealed record ChunithmBattleData(
    string? Nickname,
    Dictionary<(long Id, int LevelIdx), ChunithmScore> Scores);
