using Marisa.Plugin.Shared.Versus;

namespace Marisa.Plugin.Shared.Interface;

/// <summary>提供群内双人和多人对战；游戏负责授权、谱面与结果绘制。</summary>
public interface ICanBattleWithOthers
{
    VersusBattle Battle { get; }

    [MarisaPluginDoc("比较双方单曲成绩；不填歌曲时随机", "`@某人`，可选歌曲名、别名、ID 或难度")]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "vs", "对战")]
    Task<MarisaPluginTaskState> SongVersus(Message message) => Battle.Single(message);

    [MarisaPluginDoc("随机比较 N 个成绩", "`N [难度/范围]`（最大 20），可在参数前后 @某人")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginTrigger(typeof(ICanBattleWithOthers), nameof(VersusRandomTrigger), MessageType.GroupMessage)]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "n")]
    Task<MarisaPluginTaskState> SongVersusRandom(Message message) => Battle.Random(message);

    [MarisaPluginDoc("比较范围内的全部谱面", "`范围`，可选难度、版本、等级或定数，可在参数前后 @某人")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginTrigger(typeof(ICanBattleWithOthers), nameof(VersusBatchTrigger), MessageType.GroupMessage)]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "b")]
    Task<MarisaPluginTaskState> SongVersusBatch(Message message) => Battle.Batch(message);

    [MarisaPluginDoc("多人对战；不填歌曲时每轮随机", "可选歌曲或难度，可 @ 多人")]
    [MarisaPluginSubCommand(nameof(SongVersus))]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "开房")]
    Task<MarisaPluginTaskState> SongVersusRoom(Message message, long qq) => Battle.SingleRoom(message, qq);

    [MarisaPluginDoc("每轮随机比较 N 个成绩", "`N [难度/范围]`（最大 20），可在参数前后 @多人")]
    [MarisaPluginSubCommand(nameof(SongVersusRoom))]
    [MarisaPluginTrigger(typeof(ICanBattleWithOthers), nameof(VersusRandomTrigger), MessageType.GroupMessage)]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "n")]
    Task<MarisaPluginTaskState> SongVersusRoomRandom(Message message, long qq) => Battle.RandomRoom(message, qq);

    [MarisaPluginDoc("比较范围内的全部谱面", "`范围`，可选难度、版本、等级或定数，可在参数前后 @多人")]
    [MarisaPluginSubCommand(nameof(SongVersusRoom))]
    [MarisaPluginTrigger(typeof(ICanBattleWithOthers), nameof(VersusBatchTrigger), MessageType.GroupMessage)]
    [MarisaPluginCommand(MessageType.GroupMessage, false, "b")]
    Task<MarisaPluginTaskState> SongVersusRoomBatch(Message message, long qq) => Battle.BatchRoom(message, qq);

    bool VersusRandomTrigger(Message message, IServiceProvider provider) => VersusCommands.RandomTrigger(message);

    bool VersusBatchTrigger(Message message, IServiceProvider provider) => VersusCommands.BatchTrigger(message, Battle.CanParseScope);
}
