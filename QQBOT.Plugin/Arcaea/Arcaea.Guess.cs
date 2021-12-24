using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Arcaea;
using QQBot.Plugin.Shared.Util;
using QQBOT.Plugin.Shared.Util;

namespace QQBot.Plugin.Arcaea;

public partial class Arcaea
{
    private static MiraiPluginTaskState ProcSongGuessResult(MessageSenderProvider sender, Message message, ArcaeaSong song, ArcaeaSong? guess)
    {
        // 未知的歌，不算
        if (guess == null)
        {
            sender.SendByRecv(MessageChain.FromPlainText("没找到你说的这首歌"), message);
            return MiraiPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            sender.SendByRecv(new MessageChain(new MessageData[]
            {
                new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                ImageMessage.FromBase64(song.GetImage())
            }), message);

            return MiraiPluginTaskState.CompletedTask;
        }

        // 猜错了
        sender.SendByRecv( MessageChain.FromPlainText("不对不对！"), message);
        return MiraiPluginTaskState.ToBeContinued;
    }

    private Func<MessageSenderProvider, Message, MiraiPluginTaskState> GenGuessDialogHandler(ArcaeaSong song,
        DateTime startTime, long qq)
    {
        return (ms, message) =>
        {
            switch (message.Command)
            {
                case "结束猜曲" or "答案":
                {
                    ms.SendByRecv(new MessageChain(new MessageData[]
                    {
                        new PlainMessage($"猜曲结束，正确答案：{song.Title}"),
                        ImageMessage.FromBase64(song.GetImage()),
                        new PlainMessage(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                    }), message);
                    return MiraiPluginTaskState.CompletedTask;
                }
                case "来点提示":
                {
                    MessageChain? hint = null;
                    switch (new Random().Next(2))
                    {
                        case 0:
                            hint = MessageChain.FromPlainText($"作曲家是：{song.Author}");
                            break;
                        case 1:
                        {
                            var cover = ResourceManager.GetCover(song.CoverFileName);

                            hint = new MessageChain(new MessageData[]
                            {
                                new PlainMessage("封面裁剪："),
                                ImageMessage.FromBase64(cover.RandomCut(cover.Width / 3, cover.Height / 3).ToB64())
                            });
                            break;
                        }
                    }

                    ms.SendByRecv(hint!, message, false);

                    return MiraiPluginTaskState.ToBeContinued;
                }
            }

            if (!message.At(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MiraiPluginTaskState.NoResponse;

                // time out
                ms.SendByRecv(MessageChain.FromPlainText("阿卡伊猜曲已结束"), message, false);
                return MiraiPluginTaskState.Canceled;
            }

            var search = SearchSongByAlias(message.Command);

            var procResult =
                new Func<ArcaeaSong?, MiraiPluginTaskState>(s => ProcSongGuessResult(ms, message, song, s));

            switch (search.Count)
            {
                case 0:
                    return procResult(null);
                case 1:
                    return procResult(search[0]);
                default:
                    ms.SendByRecv(GetSearchResult(search), message);
                    return MiraiPluginTaskState.ToBeContinued;
            }
        };
    }

    private bool StartGuess(ArcaeaSong song, MessageSenderProvider ms, Message message, long qq)
    {
        var groupId    = message.GroupInfo!.Id;
        var now        = DateTime.Now;
        var res = Dialog.AddHandler(groupId,
            (sender, msg) => Task.FromResult(GenGuessDialogHandler(song, now, qq)(sender, msg)));

        if (res) return true;

        ms.SendByRecv(MessageChain.FromPlainText("？"), message);
        return false;
    }

    private void StartSongCoverGuess(Message message, MessageSenderProvider ms, long qq)
    {
        var song = SongList.RandomTake();

        var cover = ResourceManager.GetCover(song.CoverFileName);

        var cw = cover.Width  / 3;
        var ch = cover.Height / 3;

        if (StartGuess(song, ms, message, qq))
        {
            ms.SendByRecv(new MessageChain(new MessageData[]
            {
                new PlainMessage("猜曲模式启动！"),
                ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);
        }
    }
}