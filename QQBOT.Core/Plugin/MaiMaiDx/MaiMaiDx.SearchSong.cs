using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using Flurl.Http;
using Newtonsoft.Json;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.MaiMaiDx
{
    public partial class MaiMaiDx
    {
        private List<MaiMaiSong> _songList;
        private Dictionary<string, List<string>> _songAlias;

        private List<MaiMaiSong> SongList
        {
            get
            {
                if (_songList == null)
                {
                    try
                    {
                        var data = "https://www.diving-fish.com/api/maimaidxprober/music_data"
                            .GetJsonListAsync()
                            .Result;

                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data)
                        {
                            _songList.Add(new MaiMaiSong(d));
                        }
                    }
                    catch
                    {
                        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                            File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                        ) as dynamic[];
                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data)
                        {
                            _songList.Add(new MaiMaiSong(d));
                        }
                    }
                }

                return _songList;
            }
        }

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

            foreach (var line in File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.tsv")
                .Concat(File.ReadAllLines(ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt")))
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

                    var watcher = new FileSystemWatcher
                    {
                        Path = ResourceManager.ResourcePath,
                        NotifyFilter = NotifyFilters.LastWrite,
                        Filter = "aliases.tsv"
                    };

                    var processing = false;

                    watcher.Changed += (_, _) =>
                    {
                        if (processing) return;

                        lock (watcher)
                        {
                            processing = true;
                            // 考虑到文件变化时，操作文件的程序可能还未释放文件，因此进行延迟操作
                            Thread.Sleep(500);
                            _songAlias = GetSongAliases();
                            processing = false;
                        }
                    };
                    watcher.EnableRaisingEvents = true;
                }

                return _songAlias;
            }
        }

        private MessageChain GetSongInfo(long songId)
        {
            var searchResult = SongList.FirstOrDefault(song => song.Id == songId);

            if (searchResult == null)
            {
                return MessageChain.FromPlainText($"“未找到 ID 为 {songId} 的歌曲”");
            }

            return new MessageChain(new MessageData[]
            {
                new PlainMessage(searchResult.Title),
                ImageMessage.FromBase64(searchResult.GetImage())
            });
        }

        private List<MaiMaiSong> SearchSong(string alias)
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

        private List<MaiMaiSong> ListSongs(string param)
        {
            if (string.IsNullOrEmpty(param))
            {
                return SongList;
            }

            string[] subCommand =
            //      0       1          2        3      4
                { "base", "level", "charter", "bpm", "lv"};
            var res = param.CheckPrefix(subCommand).ToList();

            if (!res.Any()) return new List<MaiMaiSong>();

            var (prefix, index) = res.First();

            switch (index)
            {
                case 0: // base
                {
                    var param1 = param.TrimStart(prefix).Trim();

                    if (param1.Contains('-'))
                    {
                        if (double.TryParse(param1.Split('-')[0], out var @base1) &&
                            double.TryParse(param1.Split('-')[1], out var @base2))
                        {
                            return SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
                        }
                    }
                    else
                    {
                        if (double.TryParse(param1, out var @base))
                        {
                            return SongList.Where(s => s.Constants.Contains(@base)).ToList();
                        }
                    }
                    return new List<MaiMaiSong>();
                }
                case 4:
                case 1: // level
                {
                    var lv = param.TrimStart(prefix).Trim();
                    return SongList.Where(s => s.Levels.Contains(lv)).ToList();
                }
                case 2: // charter
                {
                    var charter = param.TrimStart(prefix).Trim();
                    return SongList
                        .Where(s => s.Charts
                            .Any(c => c.Charter.Contains(charter, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }
                case 3: // bpm
                {
                    var param1 = param.TrimStart(prefix).Trim();

                    if (param1.Contains('-'))
                    {
                        if (long.TryParse(param1.Split('-')[0], out var bpm1) &&
                            long.TryParse(param1.Split('-')[1], out var bpm2))
                        {
                            return SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
                        }
                    }
                    else
                    {
                        if (long.TryParse(param1, out var bpm))
                        {
                            return SongList.Where(s => s.Info.Bpm == bpm).ToList();
                        }
                    }
                    return new List<MaiMaiSong>();
                }
            }

            return new List<MaiMaiSong>();
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
    }
}