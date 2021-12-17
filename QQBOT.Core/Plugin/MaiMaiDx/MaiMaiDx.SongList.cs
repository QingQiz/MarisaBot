using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
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

        private List<MaiMaiSong> SongList
        {
            get
            {
                if (_songList == null)
                    try
                    {
                        var data = "https://www.diving-fish.com/api/maimaidxprober/music_data"
                            .GetJsonListAsync()
                            .Result;

                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data) _songList.Add(new MaiMaiSong(d));
                    }
                    catch
                    {
                        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                            File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                        ) as dynamic[];
                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data) _songList.Add(new MaiMaiSong(d));
                    }

                return _songList;
            }
        }

        private MessageChain GetSongInfo(long songId)
        {
            var searchResult = SongList.FirstOrDefault(song => song.Id == songId);

            if (searchResult == null) return MessageChain.FromPlainText($"“未找到 ID 为 {songId} 的歌曲”");

            return new MessageChain(new MessageData[]
            {
                new PlainMessage(searchResult.Title),
                ImageMessage.FromBase64(searchResult.GetImage())
            });
        }

        private List<MaiMaiSong> ListSongs(string param)
        {
            if (string.IsNullOrEmpty(param)) return SongList;

            string[] subCommand =
                //      0       1          2        3      4
                { "base", "level", "charter", "bpm", "lv" };
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
                            return SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
                    }
                    else
                    {
                        if (double.TryParse(param1, out var @base))
                            return SongList.Where(s => s.Constants.Contains(@base)).ToList();
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
                            return SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
                    }
                    else
                    {
                        if (long.TryParse(param1, out var bpm)) return SongList.Where(s => s.Info.Bpm == bpm).ToList();
                    }

                    return new List<MaiMaiSong>();
                }
            }

            return new List<MaiMaiSong>();
        }
    }
}