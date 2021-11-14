using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
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

        private MessageChain GetSearchResult(List<MaiMaiSong> songs)
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

        private MessageChain GetSearchResultS(List<MaiMaiSong> song)
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
            //                           0       1        2      3       4      5       6      7      8
            string[] subCommand    = { "b40", "search", "搜索", "查分", "搜歌", "song", "查歌", "id", "name"};

            msg = msg.TrimStart(commandPrefix);

            if (string.IsNullOrEmpty(msg)) return null;

            var res = msg.CheckPrefix(subCommand);

            foreach (var (prefix, index) in res)
            {
                switch (index)
                {
                    case 0:
                    case 3:
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
                    case 6:
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        return GetSearchResult(search);
                    }
                    case 7:
                        var  last = msg.TrimStart(prefix).Trim();
                        if (long.TryParse(last, out var id))
                        {
                            return GetSongInfo(id);
                        }
                        return MessageChain.FromPlainText($"“你看你输的这个几把玩意儿像不像个ID”");
                    case 8:
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        return GetSearchResultS(search);
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

        public override async Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);
        }

        public override async Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return;

            var source = message.Source.Id;

            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);
        }

        #endregion
    }
}