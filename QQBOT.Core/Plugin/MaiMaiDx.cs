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
        private Dictionary<string, string> _songAlias;

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

        private Dictionary<string, string> SongAlias
        {
            get
            {
                if (_songAlias == null)
                {
                    _songAlias = new Dictionary<string, string>();

                    foreach (var line in File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.csv"))
                    {
                        var titles = line
                            .Split('\t')
                            .Select(x => x.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

                        foreach (var title in titles)
                        {
                            _songAlias[title] = titles[0];
                        }
                    }
                }

                return _songAlias;
            }
        }

        private MessageChain GetSongInfo(string songTitle)
        {
            var searchResult = SongList.FirstOrDefault(song =>
                string.Equals(song.Title, songTitle, StringComparison.OrdinalIgnoreCase));

            if (searchResult == null)
            {
                return MessageChain.FromPlainText($"“歌曲《{songTitle}》在当前版本的舞萌中已被删除”");
            }

            
            return MessageChain.FromPlainText(searchResult.Title);
        }

        private MessageChain SearchSong(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return MessageChain.FromPlainText("啥？");
            }

            if (SongAlias.ContainsKey(name))
            {
                return GetSongInfo(SongAlias[name]);
            }

            var result = SongAlias.Keys
                .Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(k => SongAlias[k])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result.Count switch
            {
                >= 10 => MessageChain.FromPlainText($"过多的结果（{result.Count}个）"),
                0 => MessageChain.FromPlainText("“查无此歌”"),
                1 => GetSongInfo(result[0]),
                _ => MessageChain.FromPlainText(string.Join('\n', result.Select((song, i) => $"[{i + 1}] -> {song}")))
            };
        }

        #endregion
        

        #region Message Handler

        private async Task<MessageChain> Handler(string msg, MessageSenderInfo sender)
        {
            string[] commandPrefix = { "maimai", "mai", "舞萌" };
            //                           0       1        2      3       4      5       6
            string[] subCommand    = { "b40", "search", "搜索", "查分", "搜歌", "song", "查歌"};

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
                            return MessageChain.FromPlainText("“403 forbidden");
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
                        var name = msg.TrimStart(prefix).Trim();
                        return SearchSong(name);
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

            var source = (message.MessageChain!.Messages.First(m => m.Type == MessageType.Source) as SourceMessage)!.Id;

            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);
        }

        #endregion
    }
}