using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.MaiMaiDx
{
    public partial class MaiMaiDx
    {
        private Dictionary<string, List<string>> _songAlias;

        /// <summary>
        /// 监视 alias 文件变动并更新别名列表，至于为什么放在这里，见 https://stackoverflow.com/a/16279093/13442887
        /// </summary>
        private FileSystemWatcher _songAliasChangedWatcher;

        private Dictionary<string, List<string>> GetSongAliases()
        {
            var  songAlias = new Dictionary<string, List<string>>();

            foreach (var song in SongList)
            {
                if (!songAlias.ContainsKey(song.Title))
                {
                    songAlias[song.Title] = new List<string>();
                }

                songAlias[song.Title].Add(song.Title);
            }

            // 读别名列表
            var lines = File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.tsv");

            // 尝试读临时别名
            try
            {
                lines = lines.Concat(File.ReadAllLines(ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt"))
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
                    if (!songAlias.ContainsKey(title))
                    {
                        songAlias[title] = new List<string>();
                    }

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
                        Path = ResourceManager.ResourcePath,
                        NotifyFilter = NotifyFilters.LastWrite,
                        Filter = "aliases.tsv"
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

        private List<MaiMaiSong> SearchSongByAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return null;
            }

            return SongAlias.Keys
                .Where(songNameAlias => songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase))
                .SelectMany(songNameAlias => SongAlias[songNameAlias]/*song name*/)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(songName => SongList.Any(song => string.Equals(song.Title, songName, StringComparison.OrdinalIgnoreCase)))
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

        private static MessageChain GetSearchResult(IReadOnlyList<MaiMaiSong> songs)
        {
            if (songs == null)
            {
                return MessageChain.FromPlainText("啥？");
            }

            return songs.Count switch
            {
                >= 10 => MessageChain.FromPlainText($"过多的结果（{songs.Count}个）"),
                0 => MessageChain.FromPlainText("“查无此歌”"),
                1 => new MessageChain(new MessageData[]
                {
                    new PlainMessage(songs[0].Title),
                    ImageMessage.FromBase64(songs[0].GetImage())
                }),
                _ => MessageChain.FromPlainText(string.Join('\n', songs.Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}")))
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="song"></param>
        /// <returns>plain text</returns>
        private static MessageChain GetSearchResultS(IReadOnlyList<MaiMaiSong> song)
        {
            if (song == null)
            {
                return MessageChain.FromPlainText("啥？");
            }

            return song.Count switch
            {
                >= 30 => MessageChain.FromPlainText($"过多的结果（{song.Count}个）"),
                0 => MessageChain.FromPlainText("“查无此歌”"),
                1 => MessageChain.FromPlainText($"Title: {song[0].Title}\nArtist: {song[0].Info.Artist}"),
                _ => MessageChain.FromPlainText(string.Join('\n', song.Select(s => $"[T:{s.Type}, ID:{s.Id}] -> {s.Title}")))
            };
        }

        private MessageChain SongAliasHandler(string param)
        {
            string[] subCommand =
            //      0      1
                { "get", "set", };
            
            var res = param.CheckPrefix(subCommand).ToList();

            var (prefix, index) = res.First();

            switch (index)
            {
                case 0:
                {
                    var songName = param.TrimStart(prefix).Trim();

                    if (string.IsNullOrEmpty(songName))
                    {
                        return MessageChain.FromPlainText("？");
                    }

                    var songList = SearchSongByAlias(songName);

                    if (songList.Count == 1)
                    {
                        return MessageChain.FromPlainText(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(songList[0].Title))}");
                    }
                    return GetSearchResult(songList);
                }
                case 1:
                {
                    var param2 = param.TrimStart(prefix).Trim();
                    var names = param2.Split("$>");

                    if (names.Length != 2)
                    {
                        return MessageChain.FromPlainText("Failed");
                    }

                    lock (SongAlias)
                    {
                        var name  = names[0].Trim();
                        var alias = names[1].Trim();

                        if (SongList.Any(song => song.Title == name))
                        {
                            File.AppendAllText(
                                ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt", $"{name}\t{alias}\n");

                            if (SongAlias.ContainsKey(alias))
                            {
                                SongAlias[alias].Add(name);
                            }
                            else
                            {
                                SongAlias[alias] = new List<string> { name };
                            }

                            return MessageChain.FromPlainText("Success");
                        }

                        return MessageChain.FromPlainText($"不存在的歌曲：{name}");
                    }
                }
            }
            
            return null;
        }
    }
}