using System.Text.RegularExpressions;
using Marisa.Plugin.Shared.Interface;

namespace Marisa.Plugin.Shared.Util.SongDb;

public class SongDb<TSong> : ICanReset where TSong : Song
{
    private readonly string _aliasFilePath;

    private readonly Func<List<TSong>> _songListGen;
    private readonly string _tempAliasPath;

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
    public SongDb(string aliasFilePath, string tempAliasPath, Func<List<TSong>> songListGen)
    {
        _aliasFilePath = aliasFilePath;
        _tempAliasPath = tempAliasPath;

        _songListGen = songListGen;
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

    public void Reset()
    {
        _songList = null;
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

    private List<TSong> SearchSongByAlias(ReadOnlyMemory<char> alias)
    {
        if (alias.IsWhiteSpace()) return [];

        var key = SongAlias.Keys.Where(songNameAlias =>
            songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase)).ToList();

        if (key.Count != 0) return Result();

        try
        {
            var regex = new Regex(alias.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            key = SongAlias.Keys.Where(a => regex.IsMatch(a.Span)).ToList();
        }
        catch (RegexParseException) {}

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
}