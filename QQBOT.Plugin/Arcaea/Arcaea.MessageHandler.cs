using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin.Arcaea;

[MiraiPlugin(19)]
[MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "arcaea", "arc", "阿卡伊")]
public partial class Arcaea : MiraiPluginBase
{
    /// <summary>
    /// 搜歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MiraiPluginTaskState ArcaeaSearchSong(Message message, MessageSenderProvider ms)
    {
        var search = SearchSongByAlias(message.Command);

        ms.SendByRecv(GetSearchResult(search), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 猜歌
    /// </summary>
    [MiraiPluginCommand(MiraiMessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, true, "猜歌", "猜曲", "guess")]
    private MiraiPluginTaskState ArcaeaGuess(Message message, MessageSenderProvider ms, long qq)
    {
        StartSongCoverGuess(message, ms, qq);
        return MiraiPluginTaskState.CompletedTask;
    }

    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, false, "alias")]
    private MiraiPluginTaskState ArcaeaSongAlias(Message message, MessageSenderProvider ms)
    {
        var mc = SongAliasHandler(message.Command);
        
        if (mc != null) ms.SendByRecv(mc, message);

        return MiraiPluginTaskState.CompletedTask;
    }
}