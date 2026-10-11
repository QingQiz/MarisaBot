using System.Text.RegularExpressions;
using Marisa.Plugin.Shared.Interface;

namespace Marisa.Plugin.Shared.Util.SongDb;

public class SongDb<TSong> : ICanReset where TSong : Song
{
    private readonly string _aliasFilePath;

    private readonly Func<List<TSong>> _songListGen;
    private readonly string _tempAliasPath;
    private readonly object _cacheLock = new();

    private Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>? _songAlias;

    /// <summary>
    ///     监视 alias 文件变动并更新别名列表，至于为什么放在这里，见 https://stackoverflow.com/a/16279093/13442887
    /// </summary>
    private FileSystemWatcher? _songAliasChangedWatcher;

    private Dictionary<long, TSong>? _songIndexer;

    private List<TSong>? _songList;

    /// <param name="aliasFilePath">歌曲的别名文件路径，格式是tsv</param>
    /// <param name="tempAliasPath">歌曲临时别名的存放路径</param>
    /// <param name="songListGen">读取歌曲列表的函数</param>
    public SongDb(string aliasFilePath, string tempAliasPath, Func<List<TSong>> songListGen)
    {
        _aliasFilePath = aliasFilePath;
        _tempAliasPath = tempAliasPath;

        _songListGen = songListGen;
    }

    public List<TSong> SongList
    {
        get
        {
            lock (_cacheLock) return _songList ??= _songListGen();
        }
    }

    public Dictionary<long, TSong> SongIndexer
    {
        get
        {
            lock (_cacheLock) return _songIndexer ??= SongList.ToDictionary(s => s.Id);
        }
    }

    private Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> SongAlias
    {
        get
        {
            lock (_cacheLock)
            {
                if (_songAlias is not null) return _songAlias;

                _songAlias = GetSongAliases();

                var watcher = new FileSystemWatcher
                {
                    Path         = Path.GetDirectoryName(_aliasFilePath) ?? string.Empty,
                    NotifyFilter = NotifyFilters.LastWrite,
                    Filter       = Path.GetFileName(_aliasFilePath)
                };

                var processing = 0;
                watcher.Changed += (_, _) =>
                {
                    if (Interlocked.CompareExchange(ref processing, 1, 0) != 0) return;
                    try
                    {
                        // 等待写文件的程序释放文件；延迟期间不阻塞曲库查询和刷新。
                        Thread.Sleep(500);
                        RefreshSongAliases(watcher);
                    }
                    finally
                    {
                        Volatile.Write(ref processing, 0);
                    }
                };
                _songAliasChangedWatcher = watcher;
                watcher.EnableRaisingEvents = true;

                return _songAlias;
            }
        }
    }

    public void Reset()
    {
        lock (_cacheLock) ResetCaches();
    }

    /// <summary>替换已获取的公共曲库，并重建索引和别名；别名文件与用户临时别名保持不变。</summary>
    public void ReplaceSongList(List<TSong> songs)
    {
        lock (_cacheLock)
        {
            ResetCaches();
            _songList = songs;
        }
    }

    private void ResetCaches()
    {
        // 重新生成歌单，并作废所有从它派生的缓存——否则 SongList 虽刷新，
        // SongIndexer / SongAlias 仍指向旧数据（b50 的 is_new 走 SongIndexer，会一直 stale）。
        _songList    = null;
        _songIndexer = null;

        // alias 索引重建时会新建 FileSystemWatcher，先 dispose 旧的防泄漏。
        _songAliasChangedWatcher?.Dispose();
        _songAliasChangedWatcher = null;
        _songAlias               = null;
    }

    private void RefreshSongAliases(FileSystemWatcher watcher)
    {
        lock (_cacheLock)
        {
            // Dispose 无法取消已经排队的 Changed；旧曲库的回调不能重建新缓存。
            if (!ReferenceEquals(watcher, _songAliasChangedWatcher)) return;
            _songAlias = GetSongAliases();
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
            .Distinct()
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
        if (m.IsEmpty) return SongList;

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

    /// <summary>按完整标题、别名或 ID 搜歌，不使用包含或正则匹配。</summary>
    public List<TSong> SearchSongExact(ReadOnlyMemory<char> input)
    {
        var value = input.Span.Trim();
        if (value.IsEmpty) return [];
        var valueText = value.ToString();

        if (long.TryParse(value, out var id))
        {
            var byId = SongList.Where(song => song.Id == id).ToList();
            if (byId.Count > 0) return byId;
        }

        if (value.StartsWith("id", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(value[2..].Trim(), out var explicitId))
        {
            return SongList.Where(song => song.Id == explicitId).ToList();
        }

        var exactTitles = SongList
            .Where(song => song.Title.Equals(valueText, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactTitles.Count > 0) return exactTitles;

        lock (_cacheLock)
        {
            var keys = SongAlias.Keys
                .Where(alias => alias.Span.Equals(valueText.AsSpan(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            return keys
                .SelectMany(alias => SongAlias[alias])
                .SelectMany(title => SongList.Where(song => song.Title.AsSpan().Equals(title.Span, StringComparison.Ordinal)))
                .DistinctBy(song => song.Id)
                .ToList();
        }
    }

    private List<TSong> SearchSongByAlias(ReadOnlyMemory<char> alias)
    {
        if (alias.IsWhiteSpace()) return [];

        lock (_cacheLock)
        {
            var key = SongAlias.Keys.Where(songNameAlias =>
                songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase)).ToList();

            if (key.Count != 0) return Result();

            try
            {
                var regex = UserRegex.Create(alias.ToString());
                key = SongAlias.Keys.Where(a => regex.IsMatch(a.Span)).ToList();
            }
            catch (Exception e) when (e is RegexParseException or RegexMatchTimeoutException) {}

            return Result();

            List<TSong> Result()
            {
                return key
                    // 找到真实歌曲名
                    .SelectMany(songNameAlias => SongAlias[songNameAlias] /*song name*/)
                    .Distinct(StringComparison.OrdinalIgnoreCase)
                    // 找到歌曲
                    .Select(songName => SongList.Where(s => s.Title.Equals(songName, StringComparison.Ordinal)))
                    .SelectMany(s => s)
                    .ToList();
            }
        }
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
        lock (_cacheLock)
        {
            var aliases = SongAlias
                .Where(k /* alias: [song title] */ => k.Value.Contains(name))
                .Select(k => k.Key);
            return aliases.ToList();
        }
    }

    /// <summary>
    ///     设置歌曲别名
    /// </summary>
    /// <param name="name">歌曲全称</param>
    /// <param name="alias">别名</param>
    /// <returns>成功与否</returns>
    public bool SetSongAlias(ReadOnlyMemory<char> name, ReadOnlyMemory<char> alias)
    {
        // 别名不允许含制表符/换行，否则会破坏别名 TSV 文件的行/列结构
        if (alias.Span.IndexOfAny('\t', '\n', '\r') >= 0) return false;

        lock (_cacheLock)
        {
            var songAlias = SongAlias;
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

            if (songAlias.TryGetValue(alias, out var value))
            {
                value.Add(title);
            }
            else
            {
                songAlias[alias] = [title];
            }

            return true;
        }
    }

    #endregion
}
