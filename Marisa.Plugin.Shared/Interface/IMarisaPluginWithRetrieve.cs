using Marisa.Plugin.Shared.Chunithm;
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
    [MarisaPluginDoc("搜歌，参数为：歌曲名 或 歌曲别名 或 歌曲id 或表达式（例如const>10）")]
    [MarisaPluginCommand("song", "search", "搜索")]
    async Task<MarisaPluginTaskState> SearchSong(Message message)
    {
        return await SearchSong(SongDb, message);
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

    #region Helpers

    private static Func<Song, bool> GetConstraint(Message message, List<ReadOnlyMemory<char>> expr)
    {
        List<Func<Song, int, bool>> diffConstraint = [];
        List<Func<Song, bool>>      songConstraint = [];

        foreach (var e in expr)
        {
            var key = "";
            var op  = "";
            var val = "";

            var s = e.Span;
            for (var i = 0; i < s.Length; i++)
            {
                switch (s[i])
                {
                    case '>':
                    case '<':
                        key = e[..i].ToString();
                        if (i + 1 < s.Length && s[i + 1] == '=')
                        {
                            op = e.Slice(i, 2).ToString();
                            i++;
                        }
                        else
                        {
                            op = e.Slice(i, 1).ToString();
                        }
                        break;
                    case '=':
                        key = e[..i].ToString();
                        op  = "=";
                        break;
                    default:
                        continue;
                }
                val = e[(i + 1)..].ToString();
                break;
            }

            var available = new List<string> { "Charter", "Constant", "Bpm", "Artist", "Title", "Version", "Id", "Index", "DiffName", "Level" };

            string[] docs = ["谱师", "定数", "BPM", "艺术家", "标题", "版本", "ID", "难度索引", "难度名，如：Master，future", "等级如：14，14+"];

            switch (available.FindIndex(x => x.StartsWith(key, StringComparison.InvariantCultureIgnoreCase)))
            {
                case -1:
                    var doc = string.Join("\n", available.Zip(docs).Select(x => $"- {x.First} （{x.Second}）"));
                    message.Reply($"{key}不是可用的约束条件，可用的：\n{doc}\n可用命令的非共同前缀替代。");
                    throw new ArgumentOutOfRangeException();
                case 0: // Charter
                    CheckOp(op);
                    diffConstraint.Add((song, levelIdx) => song.Charters[levelIdx].Contains(val, StringComparison.OrdinalIgnoreCase));
                    break;
                case 1: // Constant
                    if (double.TryParse(val, out var constant))
                    {
                        var result = constant;
                        diffConstraint.Add((song, levelIdx) => GetComparer<double>(op)(song.Constants[levelIdx], result));
                        break;
                    }
                    message.Reply("Constant 只能为数字");
                    throw new ArgumentOutOfRangeException();
                case 2: // Bpm
                    if (double.TryParse(val, out var bpm))
                    {
                        var result = bpm;
                        if (typeof(TSong) == typeof(ChunithmSong))
                        {
                            diffConstraint.Add((song, i) =>
                            {
                                var cmp = GetComparer<double>(op);
                                return (song as ChunithmSong)!.BpmList[i].Any(b => cmp(b, result));
                            });
                        }
                        else
                        {
                            songConstraint.Add(song => GetComparer<double>(op)(song.Bpm, result));
                        }
                        break;
                    }
                    message.Reply("Bpm 只能为数字");
                    throw new ArgumentOutOfRangeException();
                case 3: // Artist
                    CheckOp(op);
                    songConstraint.Add(song => song.Artist.Contains(val, StringComparison.OrdinalIgnoreCase));
                    break;
                case 4: // Title
                    CheckOp(op);
                    songConstraint.Add(song => song.Title.Contains(val, StringComparison.OrdinalIgnoreCase));
                    break;
                case 5: // Version
                    CheckOp(op);
                    songConstraint.Add(song => song.Version.Contains(val, StringComparison.OrdinalIgnoreCase));
                    break;
                case 6: // Id
                    if (long.TryParse(val, out var id))
                    {
                        var result = id;
                        songConstraint.Add(song => GetComparer<long>(op)(song.Id, result));
                        break;
                    }
                    message.Reply("Id 只能为数字");
                    throw new ArgumentOutOfRangeException();
                case 7: // Index
                    if (int.TryParse(val, out var index))
                    {
                        var result = index;
                        diffConstraint.Add((_, levelIdx) => GetComparer<int>(op)(levelIdx, result));
                        break;
                    }
                    message.Reply("LevelIndex 只能为数字");
                    throw new ArgumentOutOfRangeException();
                case 8: // DiffName
                    CheckOp(op);
                    diffConstraint.Add((song, levelIdx) => song.DiffNames[levelIdx].Contains(val, StringComparison.OrdinalIgnoreCase));
                    break;
                case 9: // Level
                    diffConstraint.Add((song, levelIdx) => LevelComparer(song.Levels[levelIdx], val, op));
                    break;
            }
        }

        return song =>
        {
            var diffRes = false;
            if (diffConstraint.Any())
                for (var i = 0; i < song.Constants.Count; i++)
                {
                    diffRes |= diffConstraint.All(f => f(song, i));
                }
            else
                diffRes = true;
            return diffRes && songConstraint.All(f => f(song));
        };

        Func<T, T, bool> GetComparer<T>(string op)
        {
            return op switch
            {
                ">=" => (x, y) => Comparer<T>.Default.Compare(x, y) >= 0,
                "<=" => (x, y) => Comparer<T>.Default.Compare(x, y) <= 0,
                "="  => (x, y) => Comparer<T>.Default.Compare(x, y) == 0,
                ">"  => (x, y) => Comparer<T>.Default.Compare(x, y) > 0,
                "<"  => (x, y) => Comparer<T>.Default.Compare(x, y) < 0,
                _    => throw new ArgumentOutOfRangeException()
            };
        }

        bool LevelComparer(string a, string b, string op)
        {
            var cmp  = GetComparer<int>(op);
            var aInt = a.Last() == '+' ? int.Parse(a[..^1]) : int.Parse(a);
            var bInt = b.Last() == '+' ? int.Parse(b[..^1]) : int.Parse(b);
            if (aInt != bInt)
            {
                return cmp(aInt, bInt);
            }
            if (a.Length == b.Length) return cmp(0, 0);
            return a.Last() == '+' ? cmp(1, 0) : cmp(0, 1);
        }

        void CheckOp(string op)
        {
            if (op != "=")
            {
                message.Reply("该约束可用操作符：=");
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static List<ReadOnlyMemory<char>> GetExpressions(ReadOnlyMemory<char> search)
    {
        var s   = search.Span;
        var res = new List<ReadOnlyMemory<char>>();
        for (var idx = 0; idx < s.Length; idx++)
        {
            switch (s[idx])
            {
                case '>':
                case '<':
                case '=':
                    var i = idx;
                    for (; i >= 0; i--)
                    {
                        if (s[i] != ' ') continue;
                        i++;
                        break;
                    }
                    i = i < 0 ? 0 : i;

                    var j     = idx;
                    var quote = false;
                    for (; j < s.Length; j++)
                    {
                        if (!quote && s[j] == ' ') break;
                        if (s[j] == '"') quote = !quote;
                    }
                    res.Add(search.Slice(i, j - i));
                    break;
                default:
                    continue;
            }
        }
        return res;
    }

    private static ReadOnlyMemory<char> GetKeyword(ReadOnlyMemory<char> search, List<ReadOnlyMemory<char>> expr)
    {
        return expr.Aggregate(search, (current, e) => current.Replace(e, ReadOnlyMemory<char>.Empty));
    }

    private static async Task<MarisaPluginTaskState> SearchSong<T>(SongDb<T> songDb, Message message) where T : Song
    {
        try
        {
            var expr       = GetExpressions(message.Command);
            var keyword    = GetKeyword(message.Command, expr);
            var constraint = GetConstraint(message, expr);

            var searchRes = songDb.SearchSong(keyword.Trim());

            await songDb.MultiPageSelectResult(searchRes.Where(x => constraint(x)).ToList(), message);
        }
        catch (ArgumentOutOfRangeException)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}