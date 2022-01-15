using Microsoft.VisualBasic;

namespace QQBot.Plugin.Shared.Util.SongDb;

public class SongDb<TSong> where TSong : Song
{
    private readonly string _aliasFilePath;
    private readonly string _tempAliasPath;

    private List<TSong>? _songList;
    private Dictionary<string, List<string>>? _songAlias;

    private readonly Func<List<TSong>> _songListGen;

    public SongDb(string aliasFilePath, string tempAliasPath, Func<List<TSong>> songListGen)
    {
        _aliasFilePath = aliasFilePath;
        _tempAliasPath = tempAliasPath;

        _songListGen = songListGen;
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

    private List<TSong> SearchSongByAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return new List<TSong>();

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
            .Select(songName => SongList.FirstOrDefault(song => song.Title == songName))
            .Where(x => x is not null)
            .Cast<TSong>()
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
}