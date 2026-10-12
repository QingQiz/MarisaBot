using Marisa.Plugin.Shared.Versus;

namespace Marisa.Plugin.Shared.Interface;

/// <summary>提供群内双人和多人对战；游戏负责授权、谱面与结果绘制。</summary>
public interface ICanBattleWithOthers
{
    VersusBattle Battle { get; }

    [MarisaPluginDoc("比较双方单曲成绩；不填歌曲时随机", "`@某人`，可选歌曲名、别名、ID 或难度")]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "vs", "对战")]
    Task<MarisaPluginTaskState> SongVersus(Message message) => Battle.Single(message);

    [MarisaPluginDoc("多人对战；不填歌曲时每轮随机", "可选歌曲或难度，可 @ 多人")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "开房")]
    Task<MarisaPluginTaskState> SongVersusRoom(Message message, long qq) => Battle.SingleRoom(message, qq);
}
