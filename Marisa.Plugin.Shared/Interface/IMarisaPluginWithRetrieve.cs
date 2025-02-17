using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Interface;

public interface IMarisaPluginWithRetrieve<TSong> where TSong : Song
{
    SongDb<TSong> SongDb { get; }

    #region Search

    /// <summary>
    ///     搜歌
    /// </summary>
    [MarisaPluginDoc("搜歌，参数为：歌曲名 或 歌曲别名 或 歌曲id")]
    [MarisaPluginCommand("song", "search", "搜索")]
    async Task<MarisaPluginTaskState> SearchSong(Message message)
    {
        return await SongDb.SearchSong(message);
    }

    #endregion

    #region Alias

    /// <summary>
    ///     别名处理
    /// </summary>
    [MarisaPluginDoc("别名设置和查询")]
    [MarisaPluginCommand("alias")]
    MarisaPluginTaskState SongAlias(Message message)
    {
        message.Reply("错误的命令格式");

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     获取别名
    /// </summary>
    [MarisaPluginDoc("获取别名，参数为：歌名/别名")]
    [MarisaPluginSubCommand(nameof(SongAlias))]
    [MarisaPluginCommand("get")]
    MarisaPluginTaskState SongAliasGet(Message message)
    {
        var songName = message.Command;

        if (songName.IsEmpty)
        {
            message.Reply("？");
        }

        var songList = SongDb.SearchSong(songName);

        if (songList.Count == 1)
        {
            message.Reply($"当前歌在录的别名有：{string.Join('、', SongDb.GetSongAliasesByName(songList[0].Title))}");
        }
        else
        {
            message.Reply(SongDb.GetSearchResult(songList));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     设置别名
    /// </summary>
    [MarisaPluginDoc("设置别名，参数为：歌曲原名 或 歌曲id := 歌曲别名")]
    [MarisaPluginSubCommand(nameof(SongAlias))]
    [MarisaPluginCommand("set")]
    MarisaPluginTaskState SongAliasSet(Message message)
    {
        var param = message.Command;
        var names = param.Split(":=").ToArray();

        if (names.Length != 2)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var name  = names[0].Trim();
        var alias = names[1].Trim();

        message.Reply(SongDb.SetSongAlias(name, alias) ? "Success" : $"不存在的歌曲：{name}");

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region List

    /// <summary>
    ///     给出歌曲列表
    /// </summary>
    [MarisaPluginDoc("给出符合条件的歌曲，结果过多时回复 p1、p2 等获取额外的信息")]
    [MarisaPluginCommand("list", "ls")]
    async Task<MarisaPluginTaskState> ListSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginTrigger(typeof(Triggers), nameof(Triggers.ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    async Task<MarisaPluginTaskState> ListSongBase(Message message)
    {
        await SongDb.MultiPageSelectResult(SongDb.SelectSongByBaseRange(message.Command), message);
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("charter", "谱师")]
    async Task<MarisaPluginTaskState> ListSongCharter(Message message)
    {
        await SongDb.MultiPageSelectResult(SongDb.SelectSongByCharter(message.Command), message);
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    async Task<MarisaPluginTaskState> ListSongLevel(Message message)
    {
        await SongDb.MultiPageSelectResult(SongDb.SelectSongByLevel(message.Command), message);
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("bpm")]
    async Task<MarisaPluginTaskState> ListSongBpm(Message message)
    {
        await SongDb.MultiPageSelectResult(SongDb.SelectSongByBpmRange(message.Command), message);
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(ListSong))]
    [MarisaPluginCommand("artist", "a")]
    async Task<MarisaPluginTaskState> ListSongArtist(Message message)
    {
        await SongDb.MultiPageSelectResult(SongDb.SelectSongByArtist(message.Command), message);
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region Random

    /// <summary>
    ///     随机
    /// </summary>
    [MarisaPluginDoc("随机给出一个符合条件的歌曲")]
    [MarisaPluginCommand("random", "rand", "随机")]
    async Task<MarisaPluginTaskState> RandomSong(Message message)
    {
        message.Reply("错误的命令格式");
        return await Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定定数约束的歌，参数为：定数 或 定数1-定数2")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginTrigger(typeof(Triggers), nameof(Triggers.ListBaseTrigger))]
    [MarisaPluginCommand("base", "b", "定数")]
    Task<MarisaPluginTaskState> RandomSongBase(Message message)
    {
        SongDb.SelectSongByBaseRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定谱师约束的歌，参数为：谱师")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("charter", "谱师")]
    Task<MarisaPluginTaskState> RandomSongCharter(Message message)
    {
        SongDb.SelectSongByCharter(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定等级约束的歌，参数为：等级")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("level", "lv", "等级")]
    Task<MarisaPluginTaskState> RandomSongLevel(Message message)
    {
        SongDb.SelectSongByLevel(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定BPM约束的歌，参数为：bpm 或 bmp1-bmp2")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("bpm")]
    Task<MarisaPluginTaskState> RandomSongBpm(Message message)
    {
        SongDb.SelectSongByBpmRange(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("随机给出符合指定曲师约束的歌，参数为：曲师")]
    [MarisaPluginSubCommand(nameof(RandomSong))]
    [MarisaPluginCommand("artist", "a")]
    Task<MarisaPluginTaskState> RandomSongArtist(Message message)
    {
        SongDb.SelectSongByArtist(message.Command).RandomSelectResult(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion
}