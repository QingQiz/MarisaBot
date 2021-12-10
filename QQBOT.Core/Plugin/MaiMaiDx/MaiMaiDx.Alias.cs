using System.Collections.Generic;
using System.IO;
using System.Linq;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.MaiMaiDx
{
    public partial class MaiMaiDx
    {
        private List<string> GetSongAliasesByName(string name)
        {
            var aliases = SongAlias
                .Where(k /* alias: [song title] */ => k.Value.Contains(name))
                .Select(k => k.Key);
            return aliases.ToList();
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

                    var songList = SearchSong(songName);

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