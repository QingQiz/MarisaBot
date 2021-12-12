using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.MaiMaiDx
{
    [MiraiPlugin(priority:1)]
    public partial class MaiMaiDx : PluginBase
    {
        private async IAsyncEnumerable<MessageChain> Handler(Message message)
        {
            string[] commandPrefix = { "maimai", "mai", "舞萌" };
            string[] subCommand =
                {
                //    0       1        2      3       4      5       6      7      8        9       10      11     12
                    "b40", "search", "搜索", "查分", "搜歌", "song", "查歌", "id", "name", "random", "随机", "随歌", "list",
                //    13      14     15      16
                    "rand", "猜歌", "猜曲", "alias"
                };

            var sender = message.Sender;
            var msg    = message.MessageChain!.PlainText.Trim().TrimStart(commandPrefix);

            
            if (string.IsNullOrEmpty(msg)) yield return null;

            var prefixes = msg.CheckPrefix(subCommand);

            foreach (var (prefix, index) in prefixes)
            {
                switch (index)
                {
                    case 0:
                    case 3: // b40
                        var username = msg.TrimStart(prefix).Trim();

                        MessageChain ret;
                        try
                        {
                            var rating = await MaiB40(string.IsNullOrEmpty(username)
                                ? new { qq = sender!.Id }
                                : new { username });

                            var imgB64 = rating.GetImage();


                            ret = new MessageChain(new[]
                            {
                                ImageMessage.FromBase64(imgB64)
                            });
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 400)
                        {
                            ret = MessageChain.FromPlainText("“查无此人”");
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 403)
                        {
                            ret = MessageChain.FromPlainText("“403 forbidden”");
                        }
                        catch (FlurlHttpException e)
                        {
                            ret = MessageChain.FromPlainText($"BOT在差你分的过程中炸了：\n{e}");
                        }

                        yield return ret;
                        break;
                    case 1:
                    case 2:
                    case 4:
                    case 5:
                    case 6: // search
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSongByAlias(name);
                        yield return GetSearchResult(search);
                        break;
                    }
                    case 7: // id
                    {
                        var last = msg.TrimStart(prefix).Trim();

                        yield return long.TryParse(last, out var id)
                            ? GetSongInfo(id)
                            : MessageChain.FromPlainText("“你看你输的这个几把玩意儿像不像个ID”");
                        break;
                    }
                    case 8: // name
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSongByAlias(name);
                        yield return GetSearchResultS(search);
                        break;
                    }
                    case 9:
                    case 10:
                    case 11:
                    case 13: // random
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);

                        yield return list.Count == 0
                            ? MessageChain.FromPlainText("“NULL”")
                            : MessageChain.FromBase64(list[new Random().Next(list.Count)].GetImage());
                        break;
                    }
                    case 12: // list
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);
                        var rand  = new Random();

                        if (list.Count == 0)
                        {
                            yield return MessageChain.FromPlainText("“EMPTY”");
                            break;
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

                        yield return MessageChain.FromPlainText(str);
                        break;
                    }
                    case 14:
                    case 15: // 猜歌
                    {
                        var param = msg.TrimStart(prefix).Trim();

                        foreach (var res in SongGuessMessageHandler(message, param))
                        {
                            yield return res;
                        }
                        break;
                    }
                    case 16: // alias
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        yield return SongAliasHandler(param);
                        break;
                    }
                }
            }

            yield return null;
        }

        private async Task<IAsyncEnumerable<MessageChain>> HandlerWrapper(MiraiHttpSession session, Message message)
        {
            try
            {
                return Handler(message);
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

            if (mc == null) return PluginTaskState.NoResponse;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendFriendMessage(new Message(m), message.Sender!.Id);
            }
            return proceed ? PluginTaskState.CompletedTask : PluginTaskState.NoResponse;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return PluginTaskState.NoResponse;

            var source = message.Source.Id;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendGroupMessage(new Message(m), message.GroupInfo!.Id, source);
            }
            return proceed ? PluginTaskState.CompletedTask : PluginTaskState.NoResponse;
        }
    }
}