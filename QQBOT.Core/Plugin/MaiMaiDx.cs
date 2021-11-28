using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin(priority:1)]
    public class MaiMaiDx : PluginBase
    {
        #region B40

        private static async Task<DxRating> MaiB40(object sender)
        {

            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(sender);
            var json     = await response.GetJsonAsync();

            var rating = new DxRating(json);

            return rating;
        }

        #endregion

        #region Search

        private List<MaiMaiSong> _songList;
        private Dictionary<string, List<string>> _songAlias;

        private List<MaiMaiSong> SongList
        {
            get
            {
                if (_songList == null)
                {
                    _songList = new List<MaiMaiSong>();

                    var data = "https://www.diving-fish.com/api/maimaidxprober/music_data"
                        .GetJsonListAsync()
                        .Result;

                    foreach (var d in data)
                    {
                        _songList.Add(new MaiMaiSong(d));
                    }
                }

                return _songList;
            }
        }

        private Dictionary<string, List<string>> SongAlias
        {
            get
            {
                if (_songAlias == null)
                {
                    _songAlias = new Dictionary<string, List<string>>();

                    foreach (var song in SongList)
                    {
                        if (!_songAlias.ContainsKey(song.Title))
                        {
                            _songAlias[song.Title] = new List<string>();
                        }
                        _songAlias[song.Title].Add(song.Title);
                    }

                    foreach (var line in File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.tsv"))
                    {
                        var titles = line
                            .Split('\t')
                            .Select(x => x.Trim().Trim('"').Replace("\"\"", "\""))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

                        foreach (var title in titles)
                        {
                            if (!_songAlias.ContainsKey(title))
                            {
                                _songAlias[title] = new List<string>();
                            }

                            _songAlias[title].Add(titles[0]);
                        }
                    }
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

            return MessageChain.FromBase64(searchResult.GetImage());
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
            //      0       1          2        3
                { "base", "level", "charter", "bpm"};
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
                1 => MessageChain.FromBase64(songs[0].GetImage()),
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

        #endregion
        
        #region Message Handler

        private async Task<MessageChain> Handler(string msg, MessageSenderInfo sender)
        {
            string[] commandPrefix = { "maimai", "mai", "舞萌" };
            string[] subCommand =
            //     0       1        2      3       4      5       6      7      8        9       10      11     12       13
                { "b40", "search", "搜索", "查分", "搜歌", "song", "查歌", "id", "name", "random", "随机", "随歌", "list", "rand" };

            msg = msg.TrimStart(commandPrefix);

            if (string.IsNullOrEmpty(msg)) return null;

            var res = msg.CheckPrefix(subCommand);

            foreach (var (prefix, index) in res)
            {
                switch (index)
                {
                    case 0:
                    case 3: // b40
                        var username = msg.TrimStart(prefix).Trim();

                        try
                        {
                            var rating = await MaiB40(string.IsNullOrEmpty(username)
                                ? new { qq = sender.Id }
                                : new { username });

                            var imgB64 = rating.GetImage();


                            return new MessageChain(new[]
                            {
                                ImageMessage.FromBase64(imgB64)
                            });
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 400)
                        {
                            return MessageChain.FromPlainText("“查无此人”");
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 403)
                        {
                            return MessageChain.FromPlainText("“403 forbidden”");
                        }
                        catch (FlurlHttpException e)
                        {
                            return MessageChain.FromPlainText($"BOT在差你分的过程中炸了：\n{e}");
                        }
                    case 1:
                    case 2:
                    case 4:
                    case 5:
                    case 6: // search
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        return GetSearchResult(search);
                    }
                    case 7: // id
                        var last = msg.TrimStart(prefix).Trim();

                        return long.TryParse(last, out var id)
                            ? GetSongInfo(id)
                            : MessageChain.FromPlainText($"“你看你输的这个几把玩意儿像不像个ID”");
                    case 8: // name
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        return GetSearchResultS(search);
                    }
                    case 9:
                    case 10:
                    case 11:
                    case 13: // random
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);

                        return list.Count == 0
                            ? MessageChain.FromPlainText("“NULL”")
                            : MessageChain.FromBase64(list[new Random().Next(list.Count)].GetImage());
                    }
                    case 12: // list
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);
                        var rand  = new Random();

                        if (list.Count == 0)
                        {
                            return MessageChain.FromPlainText("“EMPTY”");
                        }

                        var str = string.Join('\n',
                            list.OrderBy(_ => rand.Next())
                                .Take(15)
                                .OrderBy(x => x.Id)
                                .Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}"));

                        if (list.Count > 15)
                        {
                            str += "\n" + $"太多了（{list.Count}），随机给出15个";
                        }

                        return MessageChain.FromPlainText(str);
                    }
                }
            }

            return null;
        }

        private async Task<MessageChain> HandlerWrapper(MiraiHttpSession session, Message message)
        {
            try
            {
                var msg = message.MessageChain!.PlainText;
                return await Handler(msg, message.Sender);
            }
            catch (Exception e)
            {
                if (message.GroupInfo == null)
                {
                    await session.SendFriendMessage(
                        new Message(MessageChain.FromPlainText(e.ToString())),
                        message.Sender!.Id);
                }
                else
                {
                    await session.SendGroupMessage(
                        new Message(MessageChain.FromPlainText(e.ToString())),
                        message.GroupInfo!.Id);
                }
                throw;
            }
        }

        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return PluginTaskState.ToBeContinued;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);
            return PluginTaskState.CompletedTask;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return PluginTaskState.ToBeContinued;

            var source = message.Source.Id;

            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);
            return PluginTaskState.CompletedTask;
        }

        #endregion
    }
}