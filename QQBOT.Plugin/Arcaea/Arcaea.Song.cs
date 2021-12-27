using System.Dynamic;
using Newtonsoft.Json;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Util;
using QQBot.Plugin.Shared.Arcaea;

namespace QQBot.Plugin.Arcaea;

/// <summary>
/// 这一部分基本上都是从 maimaidx 插件复制过来的，没想好怎么把这些东西抽象出来，或者也没必要抽象出来
/// </summary>
public partial class Arcaea
{
    #region song list

    private List<ArcaeaSong>? _songList;

    private List<ArcaeaSong> SongList
    {
        get
        {
            if (_songList == null)
            {
                var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                ) as dynamic[];

                _songList = new List<ArcaeaSong>();

                foreach (var d in data) _songList.Add(new ArcaeaSong(d));
            }

            return _songList;
        }
    }

    #endregion

    #region song alias

    private Dictionary<string, List<string>>? _songAlias;

    private FileSystemWatcher? _songAliasChangedWatcher;

    private Dictionary<string, List<string>> GetSongAliases()
    {
        var songAlias = new Dictionary<string, List<string>>();

        foreach (var song in SongList)
        {
            if (!songAlias.ContainsKey(song.Title)) songAlias[song.Title] = new List<string>();

            songAlias[song.Title].Add(song.Title);
        }

        // 读别名列表
        var lines = File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.tsv");

        // 尝试读临时别名
        try
        {
            lines = lines.Concat(File.ReadAllLines(ResourceManager.TempPath + "/ArcaeaSongAliasTemp.txt"))
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

    private Dictionary<string, List<string>> SongAlias
    {
        get
        {
            if (_songAlias == null)
            {
                _songAlias = GetSongAliases();

                _songAliasChangedWatcher = new FileSystemWatcher
                {
                    Path         = ResourceManager.ResourcePath,
                    NotifyFilter = NotifyFilters.LastWrite,
                    Filter       = "aliases.tsv"
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
            }

            return _songAlias;
        }
    }

    private List<ArcaeaSong> SearchSong(string m)
    {
        if (string.IsNullOrEmpty(m)) return new List<ArcaeaSong>();

        m = m.Trim();

        var search = SearchSongByAlias(m);

        if (long.TryParse(m, out var id))
        {
            search.AddRange(SongList.Where(s => s.Id == id));
        }

        if (m.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            if (long.TryParse(m.TrimStart("id")!.Trim(), out var songId))
                search = SongList.Where(s => s.Id == songId).ToList();

        return search;
    }

    private List<ArcaeaSong> SearchSongByAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return new List<ArcaeaSong>();

        return SongAlias.Keys
            .Where(songNameAlias => songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase))
            .SelectMany(songNameAlias => SongAlias[songNameAlias] /*song name*/)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(songName =>
                SongList.Any(song => string.Equals(song.Title, songName, StringComparison.OrdinalIgnoreCase)))
            .Select(songName =>
                SongList.First(song => string.Equals(song.Title, songName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private List<string> GetSongAliasesByName(string name)
    {
        var aliases = SongAlias
            .Where(k /* alias: [song title] */ => k.Value.Contains(name))
            .Select(k => k.Key);
        return aliases.ToList();
    }

    private static MessageChain GetSearchResult(IReadOnlyList<ArcaeaSong> songs)
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
                songs.Select(song => $"[ID:{song.Id}] -> {song.Title}")))
        };
    }

    #endregion
}