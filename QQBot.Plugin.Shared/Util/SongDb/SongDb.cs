using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.Shared;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using static QQBot.Plugin.Shared.Dialog.Dialog;

namespace QQBot.Plugin.Shared.Util.SongDb;

public class SongDb<TSong, TSongGuess> where TSong : Song where TSongGuess : SongGuess, new()
{
    private readonly string _aliasFilePath;
    private readonly string _tempAliasPath;

    private List<TSong>? _songList;
    private Dictionary<string, List<string>>? _songAlias;

    private readonly Func<List<TSong>> _songListGen;
    private readonly string _guessDbSetName;

    private readonly MessageHandlerAdder _songGuessHandlerAdder;

    /// <param name="aliasFilePath">歌曲的别名文件路径，格式是tsv</param>
    /// <param name="tempAliasPath">歌曲临时别名的存放路径</param>
    /// <param name="songListGen">读取歌曲列表的函数</param>
    /// <param name="guessGuessDbSetName">猜歌DbSet名称</param>
    /// <param name="songGuessHandlerAdder">添加猜曲结果处理器的函数</param>
    public SongDb(string aliasFilePath, string tempAliasPath, Func<List<TSong>> songListGen,
        string guessGuessDbSetName, MessageHandlerAdder songGuessHandlerAdder)
    {
        _aliasFilePath = aliasFilePath;
        _tempAliasPath = tempAliasPath;

        _songListGen = songListGen;

        _guessDbSetName        = guessGuessDbSetName;
        _songGuessHandlerAdder = songGuessHandlerAdder;
    }

    private Dictionary<string, List<string>> GetSongAliases()
    {
        var songAlias = new Dictionary<string, List<string>>();

        foreach (var song in SongList)
        {
            if (!songAlias.ContainsKey(song.Title)) songAlias[song.Title] = new List<string>();

            songAlias[song.Title].Add(song.Title);
        }

        // 读别名列表
        var lines = File.ReadAllLines(_aliasFilePath);

        // 尝试读临时别名
        try
        {
            lines = lines.Concat(File.ReadAllLines(_tempAliasPath))
                .ToArray();
        }
        catch (FileNotFoundException)
        {
        }

        foreach (var line in lines)
        {
            var titles = line
                .Split('\t')
                .Select(x => x.Trim().Trim('"').Replace("\"\"", "\""))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            foreach (var title in titles)
            {
                if (!songAlias.ContainsKey(title)) songAlias[title] = new List<string>();

                songAlias[title].Add(titles[0]);
            }
        }

        return songAlias;
    }

    /// <summary>
    /// 监视 alias 文件变动并更新别名列表，至于为什么放在这里，见 https://stackoverflow.com/a/16279093/13442887
    /// </summary>
    private FileSystemWatcher _songAliasChangedWatcher = null!;

    public List<TSong> SongList => _songList ??= _songListGen();

    private Dictionary<string, List<string>> SongAlias
    {
        get
        {
            if (_songAlias is not null) return _songAlias;

            _songAlias = GetSongAliases();

            _songAliasChangedWatcher = new FileSystemWatcher
            {
                Path         = Path.GetDirectoryName(_aliasFilePath) ?? string.Empty,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter       = Path.GetFileName(_aliasFilePath)
            };

            var processing = false;

            _songAliasChangedWatcher.Changed += (_, _) =>
            {
                if (processing) return;

                lock (_songAlias)
                {
                    processing = true;
                    // 考虑到文件变化时，操作文件的程序可能还未释放文件，因此进行延迟操作
                    Thread.Sleep(500);
                    _songAlias = GetSongAliases();
                    processing = false;
                }
            };
            _songAliasChangedWatcher.EnableRaisingEvents = true;
            GC.KeepAlive(_songAliasChangedWatcher);

            return _songAlias;
        }
    }

    public List<TSong> SearchSong(string m)
    {
        if (string.IsNullOrEmpty(m)) return new List<TSong>();

        m = m.Trim();

        var search = SearchSongByAlias(m);

        if (long.TryParse(m, out var id))
        {
            search.AddRange(SongList.Where(s => s.Id == id));
        }

        // ReSharper disable once InvertIf
        if (m.StartsWith("id", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(m[2..].Trim(), out var songId))
            {
                search = SongList.Where(s => s.Id == songId).ToList();
            }
        }

        return search;
    }

    private TSong? SearchSongByAliasWholeWord(string alias)
    {
        var key = SongAlias.Keys.FirstOrDefault(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase));

        if (key is null) return null;

        var names = SongAlias[key];

        if (names.Count != 1) return null;
        
        var songs = SongList.Where(song => song.Title == names[0]).ToList();

        return songs.Count == 1 ? songs[0] : null;
    }

    private List<TSong> SearchSongByAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return new List<TSong>();

        if (SearchSongByAliasWholeWord(alias) is { } song)
        {
            return new List<TSong> {song};
        }

#pragma warning disable CA1416
        alias = Strings.StrConv(alias, VbStrConv.SimplifiedChinese)!;
        return SongAlias.Keys
            // 找到别名匹配的
            .Where(songNameAlias => Strings.StrConv(songNameAlias, VbStrConv.SimplifiedChinese)!
                .Contains(alias, StringComparison.OrdinalIgnoreCase))
            // 找到真实歌曲名
            .SelectMany(songNameAlias => SongAlias[songNameAlias] /*song name*/)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            // 找到歌曲
            .Select(songName => SongList.Where(s => s.Title == songName))
            .SelectMany(s => s)
            .ToList();
#pragma warning restore CA1416
    }

    /// <summary>
    /// 获取歌曲的别名列表
    /// </summary>
    /// <param name="name">歌曲全称</param>
    /// <returns>别名列表</returns>
    public IEnumerable<string> GetSongAliasesByName(string name)
    {
        var aliases = SongAlias
            .Where(k /* alias: [song title] */ => k.Value.Contains(name))
            .Select(k => k.Key);
        return aliases.ToList();
    }

    /// <summary>
    /// 设置歌曲别名
    /// </summary>
    /// <param name="name">歌曲全称</param>
    /// <param name="alias">别名</param>
    /// <returns>成功与否</returns>
    public bool SetSongAlias(string name, string alias)
    {
        if (SongList.All(song => song.Title != name)) return false;

        lock (SongAlias)
        {
            File.AppendAllText(_tempAliasPath, $"{name}\t{alias}\n");

            if (SongAlias.ContainsKey(alias))
            {
                SongAlias[alias].Add(name);
            }
            else
            {
                SongAlias[alias] = new List<string> { name };
            }

            return true;
        }
    }

    public MessageChain GetSearchResult(IReadOnlyList<TSong> songs)
    {
        return songs.Count switch
        {
            >= 10 => MessageChain.FromPlainText($"过多的结果（{songs.Count}个）"),
            0     => MessageChain.FromPlainText("“查无此歌”"),
            1 => new MessageChain(new MessageData[]
            {
                new PlainMessage(songs[0].Title),
                ImageMessage.FromBase64(songs[0].GetImage())
            }),
            _ => MessageChain.FromPlainText(string.Join('\n',
                songs.Select(song => $"[ID:{song.Id}, Lv:{song.MaxLevel()}] -> {song.Title}")))
        };
    }

    #region Guess

    private async Task<MiraiPluginTaskState> ProcSongGuessResult(MessageSenderProvider sender, Message message,
        TSong song, TSong? guess)
    {
        var dbContext  = new BotDbContext();
        var senderId   = message.Sender!.Id;
        var senderName = message.Sender!.Name;

        var db   = (DbSet<TSongGuess>)dbContext.GetType().GetProperty(_guessDbSetName)!.GetValue(dbContext, null)!;
        var @new = db.Any(u => u.UId == senderId);
        var u = @new
            ? db.First(u => u.UId == senderId)
            : new SongGuess(senderId, senderName).CastTo<TSongGuess>();

        // 未知的歌，不算
        if (guess == null)
        {
            sender.Reply(MessageChain.FromPlainText("没找到你说的这首歌"), message);
            return MiraiPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            u.TimesCorrect++;
            u.Name = senderName;
            db.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            sender.Reply(new MessageChain(new MessageData[]
            {
                new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                ImageMessage.FromBase64(song.GetImage())
            }), message);

            return MiraiPluginTaskState.CompletedTask;
        }

        // 猜错了
        u.TimesWrong++;
        u.Name = senderName;
        db.InsertOrUpdate(u);
        await dbContext.SaveChangesAsync();

        sender.Reply(MessageChain.FromPlainText("不对不对！"), message);
        return MiraiPluginTaskState.ToBeContinued;
    }

    private Func<MessageSenderProvider, Message, Task<MiraiPluginTaskState>> GenGuessDialogHandler(TSong song,
        DateTime startTime, long qq)
    {
        return async (ms, message) =>
        {
            switch (message.Command)
            {
                case "结束猜曲" or "答案":
                {
                    ms.Reply(new MessageChain(new MessageData[]
                    {
                        new PlainMessage($"猜曲结束，正确答案：{song.Title}"),
                        ImageMessage.FromBase64(song.GetImage()),
                        new PlainMessage(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                    }), message);
                    return MiraiPluginTaskState.CompletedTask;
                }
                case "来点提示":
                {
                    MessageChain? hint = null;
                    switch (new Random().Next(3))
                    {
                        case 0:
                            hint = MessageChain.FromPlainText($"作曲家是：{song.Artist}");
                            break;
                        case 1:
                            hint = MessageChain.FromPlainText($"是个{song.MaxLevel()}");
                            break;
                        case 2:
                        {
                            var cover = song.GetCover();

                            hint = new MessageChain(new MessageData[]
                            {
                                new PlainMessage("封面裁剪："),
                                ImageMessage.FromBase64(cover.RandomCut(cover.Width / 3, cover.Height / 3).ToB64())
                            });
                            break;
                        }
                    }

                    ms.Reply(hint!, message, false);

                    return MiraiPluginTaskState.ToBeContinued;
                }
            }

            if (!message.At(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MiraiPluginTaskState.NoResponse;

                // time out
                ms.Reply(MessageChain.FromPlainText("猜曲已结束"), message, false);
                return MiraiPluginTaskState.Canceled;
            }

            var search = SearchSong(message.Command).DistinctBy(s => s.Title).ToList();

            var procResult =
                new Func<TSong?, Task<MiraiPluginTaskState>>(s => ProcSongGuessResult(ms, message, song, s));

            switch (search.Count)
            {
                case 0:
                    return await procResult(null);
                case 1:
                    return await procResult(search[0]);
                default:
                    ms.Reply(GetSearchResult(search), message);
                    return MiraiPluginTaskState.ToBeContinued;
            }
        };
    }

    public bool StartGuess(TSong song, MessageSenderProvider ms, Message message, long qq)
    {
        var senderId = message.Sender!.Id;
        var senderName = message.Sender!.Name;
        var groupId = message.GroupInfo!.Id;
        var now = DateTime.Now;
        var res = _songGuessHandlerAdder(groupId, (sender, msg) => GenGuessDialogHandler(song, now, qq)(sender, msg));

        if (!res)
        {
            ms.Reply(MessageChain.FromPlainText("？"), message);
            return false;
        }

        using var dbContext = new BotDbContext();

        var db = (DbSet<TSongGuess>)dbContext.GetType().GetProperty(_guessDbSetName)!.GetValue(dbContext, null)!;
        if (db.Any(g => g.UId == senderId))
        {
            var g = db.First(g => g.UId == senderId);
            g.Name       =  senderName;
            g.TimesStart += 1;
            dbContext.Update(g);
        }
        else
        {
            db.Add(new SongGuess(senderId, senderName)
            {
                TimesStart = 1
            }.CastTo<TSongGuess>());
        }

        dbContext.SaveChanges();

        return true;
    }

    public void StartSongCoverGuess(Message message, MessageSenderProvider ms, long qq, int widthDiv,
        Func<TSong, bool>? filter)
    {
        var songs = SongList.Where(s => filter?.Invoke(s) ?? true).ToList();

        if (!songs.Any())
        {
            ms.Reply(MessageChain.FromPlainText("None"), message);
            return;
        }

        var song = songs.RandomTake();

        var cover = song.GetCover();

        var cw = cover.Width  / widthDiv;
        var ch = cover.Height / widthDiv;

        if (StartGuess(song, ms, message, qq))
        {
            ms.Reply(new MessageChain(new MessageData[]
            {
                new PlainMessage("猜曲模式启动！"),
                ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);
        }
    }

    #endregion
}