using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.Plugin.Shared.Arcaea;
using QQBot.Plugin.Shared.Util;
using QQBOT.Plugin.Shared.Util;

namespace QQBot.Plugin.Arcaea
{
    public partial class Arcaea
    {
        private static async Task<MiraiPluginTaskState> ProcSongGuessResult(MiraiHttpSession session, Message msg, ArcaeaSong song, ArcaeaSong? guess)
        {
            // 未知的歌，不算
            if (guess == null)
            {
                await session.SendGroupMessage(
                    new Message(MessageChain.FromPlainText("没找到你说的这首歌")), msg.GroupInfo!.Id, msg.Source.Id);
                return MiraiPluginTaskState.ToBeContinued;
            }

            // 猜对了
            if (guess.Title == song.Title)
            {
                await session.SendGroupMessage(new Message(new MessageData[]
                {
                    new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                    ImageMessage.FromBase64(song.GetImage())
                }), msg.GroupInfo!.Id, msg.Source.Id);

                return MiraiPluginTaskState.CompletedTask;
            }

            // 猜错了
            await session.SendGroupMessage(
                new Message(MessageChain.FromPlainText("不对不对！")), msg.GroupInfo!.Id, msg.Source.Id);
            return MiraiPluginTaskState.ToBeContinued;
        }

        private Func<MiraiHttpSession, Message, Task<MiraiPluginTaskState>> GenGuessDialogHandler(ArcaeaSong song,
            long groupId, DateTime startTime)
        {
            return async (session, msg) =>
            {
                var m = msg.MessageChain!.PlainText.Trim();

                switch (m)
                {
                    case "结束猜曲" or "答案":
                    {
                        await session.SendGroupMessage(new Message(new MessageData[]
                        {
                            new PlainMessage($"猜曲结束，正确答案：{song.Title}"),
                            ImageMessage.FromBase64(song.GetImage()),
                            new PlainMessage(
                                $"当前歌在录的别名有：{string.Join((string?)", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                        }), groupId);
                        return MiraiPluginTaskState.CompletedTask;
                    }
                    case "来点提示":
                        switch (new Random().Next(2))
                        {
                            case 0:
                                await session.SendGroupMessage(
                                    new Message(MessageChain.FromPlainText($"作曲家是：{song.Author}")), groupId);
                                return MiraiPluginTaskState.ToBeContinued;
                            case 1:
                            {
                                var cover = ResourceManager.GetCover(song.CoverFileName);

                                await session.SendGroupMessage(new Message(new MessageData[]
                                {
                                    new PlainMessage("封面裁剪："),
                                    ImageMessage.FromBase64(cover.RandomCut(cover.Width / 3, cover.Height / 3).ToB64())
                                }), groupId);
                                return MiraiPluginTaskState.ToBeContinued;
                            }
                        }

                        break;
                }

                if (!msg.At(session.Id))
                {
                    // continue
                    if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MiraiPluginTaskState.NoResponse;

                    // time out
                    await session.SendGroupMessage(new Message(MessageChain.FromPlainText("阿卡伊猜曲已结束")), groupId);
                    return MiraiPluginTaskState.CompletedTask;
                }

                var search = SearchSongByAlias(m);

                if (long.TryParse(m, out var id))
                {
                    search.AddRange(SongList.Where(s => s.Id == id));
                }

                if (m.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                {
                    if (long.TryParse(m.TrimStart("id")!.Trim(), out var songId))
                    {
                        search = SongList.Where(s => s.Id == songId).ToList();
                    }
                }

                var procResult =
                    new Func<ArcaeaSong?, Task<MiraiPluginTaskState>>(s => ProcSongGuessResult(session, msg, song, s));

                switch (search.Count)
                {
                    case 0:
                        return await procResult(null);
                    case 1:
                        return await procResult(search[0]);
                    default:
                        await session.SendGroupMessage(new Message(GetSearchResult(search)), groupId, msg.Source.Id);
                        return MiraiPluginTaskState.ToBeContinued;
                }
            };
        }

        private MessageChain? StartGuess(long groupId, ArcaeaSong song)
        {
            var now = DateTime.Now;
            var res = Dialog.AddHandler(groupId, 
                (session, msg) => GenGuessDialogHandler(song, groupId, now)(session, msg));

            return res ? null : MessageChain.FromPlainText("？");
        }

        private MessageChain StartSongCoverGuess(Message message)
        {
            var groupId = message.GroupInfo!.Id;

            var song = SongList.RandomTake();

            var cover = ResourceManager.GetCover(song.CoverFileName);

            var cw = cover.Width  / 4;
            var ch = cover.Height / 4;

            var mc = StartGuess(groupId, song);

            if (mc == null)
                return new MessageChain(new MessageData[]
                {
                    new PlainMessage("猜曲模式启动！"),
                    ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                    new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
                });
            else
                // 你妈的，不能删这个 else，万一哪天改成 yield return 这就成了一个隐藏的大黑锅
                return mc;
        }
    }
}