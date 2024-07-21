using System.Text.RegularExpressions;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Marisa.Utils;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Processing;
using static Marisa.Plugin.Shared.Dialog.Dialog;

namespace Marisa.Plugin.Shared.Util.SongDb;

public class SongDb<TSong, TSongGuess> where TSong : Song where TSongGuess : SongGuess, new()
{
    private readonly string _aliasFilePath;
    private readonly string _guessDbSetName;

    private readonly Func<List<TSong>> _songListGen;
    private readonly string _tempAliasPath;

    public readonly MessageHandlerAdder MessageHandlerAdder;
    private Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>? _songAlias;

    /// <summary>
    ///     监视 alias 文件变动并更新别名列表，至于为什么放在这里，见 https://stackoverflow.com/a/16279093/13442887
    /// </summary>
    private FileSystemWatcher _songAliasChangedWatcher = null!;

    private Dictionary<long, TSong>? _songIndexer;

    private List<TSong>? _songList;

    /// <param name="aliasFilePath">歌曲的别名文件路径，格式是tsv</param>
    /// <param name="tempAliasPath">歌曲临时别名的存放路径</param>
    /// <param name="songListGen">读取歌曲列表的函数</param>
    /// <param name="guessGuessDbSetName">猜歌DbSet名称</param>
    /// <param name="messageHandlerAdder">添加猜曲结果处理器的函数</param>
    public SongDb(
        string aliasFilePath, string tempAliasPath, Func<List<TSong>> songListGen,
        string guessGuessDbSetName, MessageHandlerAdder messageHandlerAdder)
    {
        _aliasFilePath = aliasFilePath;
        _tempAliasPath = tempAliasPath;

        _songListGen = songListGen;

        _guessDbSetName     = guessGuessDbSetName;
        MessageHandlerAdder = messageHandlerAdder;
    }

    public List<TSong> SongList => _songList ??= _songListGen();

    public Dictionary<long, TSong> SongIndexer => _songIndexer ??= SongList.ToDictionary(s => s.Id);

    private Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> SongAlias
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

    private Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> GetSongAliases()
    {
        var songAlias = new Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>(new MemoryExt.ReadOnlyMemoryCharComparer());

        foreach (var title in SongList.Select(song => song.Title.AsMemory()))
        {
            if (!songAlias.ContainsKey(title)) songAlias[title] = [];

            songAlias[title].Add(title);
        }

        // 读别名列表
        var lines = File.ReadAllLines(_aliasFilePath).Select(x => x.AsMemory()).ToList();

        // 尝试读临时别名
        try
        {
            lines = lines.Concat(File.ReadAllLines(_tempAliasPath).Select(x => x.AsMemory())).ToList();
        }
        catch (FileNotFoundException) {}

        var songNameAll = SongList
            .Select(s => s.Title.AsMemory())
            .Distinct(StringComparison.Ordinal)
            .ToHashSet(new MemoryExt.ReadOnlyMemoryCharComparer());

        foreach (var line in lines)
        {
            var titles = line
                .Split('\t')
                .Select(x => x.Trim().UnEscapeTsvCell())
                .ToList();

            titles = titles
                .Take(1)
                .Concat(titles
                    .Skip(1)
                    .Where(x => !x.IsWhiteSpace())
                )
                .ToList();

            // 跳过被删除了的歌
            if (titles.Count != 0 && !songNameAll.Contains(titles[0]))
            {
                continue;
            }

            foreach (var title in titles)
            {
                if (!songAlias.ContainsKey(title)) songAlias[title] = [];

                songAlias[title].Add(titles[0]);
            }
        }

        return songAlias;
    }

    /// <summary>
    ///     使用歌曲 id 建立索引
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public TSong FindSong(long id)
    {
        return SongIndexer[id];
    }

    #region Search

    public TSong? GetSongById(long id)
    {
        return SongList.FirstOrDefault(song => song.Id == id);
    }

    public List<TSong> SearchSong(ReadOnlyMemory<char> input)
    {
        var m = input.Span;
        if (m.IsEmpty) return [];

        m = m.Trim();

        var search = SearchSongByAlias(input);

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

    private List<TSong> SearchSongByAlias(ReadOnlyMemory<char> alias)
    {
        if (alias.IsWhiteSpace()) return [];

        IEnumerable<ReadOnlyMemory<char>> key;

        try
        {
            var regex = new Regex(alias.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            key = SongAlias.Keys.Where(a => regex.IsMatch(a.Span));
        }
        catch (RegexParseException)
        {
            key = SongAlias.Keys.Where(songNameAlias =>
                songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase));
        }

        return key
            // 找到真实歌曲名
            .SelectMany(songNameAlias => SongAlias[songNameAlias] /*song name*/)
            .Distinct(StringComparison.OrdinalIgnoreCase)
            // 找到歌曲
            .Select(songName => SongList.Where(s => s.Title.Equals(songName, StringComparison.Ordinal)))
            .SelectMany(s => s)
            .ToList();
    }

    public MessageChain GetSearchResult(IReadOnlyList<TSong> songs)
    {
        return songs.Count switch
        {
            >= SongDbConfig.PageSize => MessageChain.FromText($"过多的结果（{songs.Count}个）"),

            0 => MessageChain.FromText("“查无此歌”"),
            1 => new MessageChain(
                new MessageDataText(songs[0].Title),
                MessageDataImage.FromBase64(songs[0].GetImage())
            ),
            _ => MessageChain.FromText(string.Join('\n',
                songs.Select(song => $"[ID:{song.Id}, Lv:{song.MaxLevel()}] -> {song.Title}")))
        };
    }

    #endregion

    #region Alias

    /// <summary>
    ///     获取歌曲的别名列表
    /// </summary>
    /// <param name="name">歌曲全称</param>
    /// <returns>别名列表</returns>
    public IEnumerable<ReadOnlyMemory<char>> GetSongAliasesByName(string name)
    {
        var aliases = SongAlias
            .Where(k /* alias: [song title] */ => k.Value.Contains(name))
            .Select(k => k.Key);
        return aliases.ToList();
    }

    /// <summary>
    ///     设置歌曲别名
    /// </summary>
    /// <param name="name">歌曲全称</param>
    /// <param name="alias">别名</param>
    /// <returns>成功与否</returns>
    public bool SetSongAlias(ReadOnlyMemory<char> name, ReadOnlyMemory<char> alias)
    {
        lock (SongAlias)
        {
            ReadOnlyMemory<char> title;
            if (long.TryParse(name.Span, out var id))
            {
                var song = GetSongById(id);
                if (song == null) return false;

                title = song.Title.AsMemory();
            }
            else
            {
                if (SongList.All(song => !song.Title.Equals(name, StringComparison.Ordinal))) return false;
                title = name;
            }

            File.AppendAllText(_tempAliasPath, $"{title}\t{alias}\n");

            if (SongAlias.TryGetValue(alias, out var value))
            {
                value.Add(title);
            }
            else
            {
                SongAlias[alias] = [title];
            }

            return true;
        }
    }

    #endregion

    #region Guess

    private async Task<MarisaPluginTaskState> ProcSongGuessResult(Message message, TSong song, TSong? guess)
    {
        var dbContext  = new BotDbContext();
        var senderId   = message.Sender.Id;
        var senderName = message.Sender.Name;

        var db   = (DbSet<TSongGuess>)dbContext.GetType().GetProperty(_guessDbSetName)!.GetValue(dbContext, null)!;
        var @new = db.Any(u => u.UId == senderId);
        var u = @new
            ? db.First(u => u.UId == senderId)
            : new SongGuess(senderId, senderName).CastTo<TSongGuess>();

        // 未知的歌，不算
        if (guess == null)
        {
            message.Reply("没找到你说的这首歌");
            return MarisaPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            u.TimesCorrect++;
            u.Name = senderName;
            db.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            message.Reply(
                new MessageDataText($"你猜对了！正确答案：{song.Title}"),
                MessageDataImage.FromBase64(song.GetImage())
            );

            return MarisaPluginTaskState.CompletedTask;
        }

        // 猜错了
        u.TimesWrong++;
        u.Name = senderName;
        db.InsertOrUpdate(u);
        await dbContext.SaveChangesAsync();

        message.Reply("不对不对！");
        return MarisaPluginTaskState.ToBeContinued;
    }

    private MessageHandler GenGuessDialogHandler(TSong song, DateTime startTime, long qq)
    {
        return async message =>
        {
            switch (message.Command.Span)
            {
                case "结束猜曲" or "答案":
                {
                    message.Reply(
                        new MessageDataText($"猜曲结束，正确答案：{song.Title}"),
                        MessageDataImage.FromBase64(song.GetImage()),
                        new MessageDataText(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                    );
                    return MarisaPluginTaskState.CompletedTask;
                }
                case "来点提示":
                {
                    MessageChain? hint = null;
                    switch (new Random().Next(3))
                    {
                        case 0:
                            hint = MessageChain.FromText($"作曲家是：{song.Artist}");
                            break;
                        case 1:
                            hint = MessageChain.FromText($"是个{song.MaxLevel()}");
                            break;
                        case 2:
                        {
                            var cover = song.GetCover();
                            cover.Mutate(i => i.RandomCut(cover.Width / 3, cover.Height / 3));

                            hint = new MessageChain(
                                new MessageDataText("封面裁剪："),
                                MessageDataImage.FromBase64(cover.ToB64())
                            );
                            break;
                        }
                    }

                    message.Reply(hint!, false);

                    return MarisaPluginTaskState.ToBeContinued;
                }
            }

            if (!message.IsAt(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MarisaPluginTaskState.NoResponse;

                // time out
                message.Reply("猜曲已结束", false);
                return MarisaPluginTaskState.Canceled;
            }

            var search = SearchSong(message.Command).DistinctBy(s => s.Title).ToList();

            var procResult =
                new Func<TSong?, Task<MarisaPluginTaskState>>(s => ProcSongGuessResult(message, song, s));

            switch (search.Count)
            {
                case 0:
                    return await procResult(null);
                case 1:
                    return await procResult(search[0]);
                default:
                    message.Reply(GetSearchResult(search));
                    return MarisaPluginTaskState.ToBeContinued;
            }
        };
    }

    public bool StartGuess(TSong song, Message message, long qq)
    {
        var senderId   = message.Sender.Id;
        var senderName = message.Sender.Name;
        var groupId    = message.GroupInfo!.Id;
        var now        = DateTime.Now;
        var res        = MessageHandlerAdder(groupId, null, msg => GenGuessDialogHandler(song, now, qq)(msg));

        if (!res)
        {
            message.Reply("？");
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

    public void StartSongCoverGuess(
        Message message, long qq, int widthDiv,
        Func<TSong, bool>? filter)
    {
        var songs = SongList.Where(s => filter?.Invoke(s) ?? true).ToList();

        if (songs.Count == 0)
        {
            message.Reply("None");
            return;
        }

        var song = songs.RandomTake();

        var cover = song.GetCover();

        var cw = cover.Width / widthDiv;
        var ch = cover.Height / widthDiv;

        cover.Mutate(i => i.RandomCut(cw, ch));

        if (StartGuess(song, message, qq))
        {
            message.Reply(
                new MessageDataText("猜曲模式启动！"),
                MessageDataImage.FromBase64(cover.ToB64()),
                new MessageDataText("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            );
        }
    }

    #endregion
}