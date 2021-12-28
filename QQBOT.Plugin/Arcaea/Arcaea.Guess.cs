using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.Arcaea;
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
    private static async Task<MiraiPluginTaskState> ProcSongGuessResult(MessageSenderProvider sender, Message message, ArcaeaSong song, ArcaeaSong? guess)
    {
        var dbContext  = new BotDbContext();
        var senderId   = message.Sender!.Id;
        var senderName = message.Sender!.Name;

        var @new = dbContext.ArcaeaGuesses.Any(u => u.UId == senderId);
        var u = @new
            ? dbContext.ArcaeaGuesses.First(u => u.UId == senderId)
            : new ArcaeaGuess(senderId, senderName);

        // 未知的歌，不算
        if (guess == null)
        {
            sender.Send("没找到你说的这首歌", message);
            return MiraiPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            u.TimesCorrect++;
            u.Name = senderName;
            dbContext.ArcaeaGuesses.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            sender.Send(new MessageChain(new MessageData[]
            {
                new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                ImageMessage.FromBase64(song.GetImage())
            }), message);

            return MiraiPluginTaskState.CompletedTask;
        }

        // 猜错了
        u.TimesWrong++;
        u.Name = senderName;
        dbContext.ArcaeaGuesses.InsertOrUpdate(u);
        await dbContext.SaveChangesAsync();

        sender.Send("不对不对！", message);
        return MiraiPluginTaskState.ToBeContinued;
    }

    private Func<MessageSenderProvider, Message, Task<MiraiPluginTaskState>> GenGuessDialogHandler(ArcaeaSong song,
        DateTime startTime, long qq)
    {
        return async (ms, message) =>
        {
            switch (message.Command)
            {
                case "结束猜曲" or "答案":
                {
                    ms.Send(new MessageChain(new MessageData[]
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
                    switch (new Random().Next(3))
                    {
                        case 0:
                            hint = MessageChain.FromPlainText($"作曲家是：{song.Author}");
                            break;
                        case 1:
                            hint = MessageChain.FromPlainText($"是个{song.Level.Last()}");
                            break;
                        case 2:
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

                    ms.Send(hint!, message, false);

                    return MiraiPluginTaskState.ToBeContinued;
                }
            }

            if (!message.At(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MiraiPluginTaskState.NoResponse;

                // time out
                ms.Send("猜曲已结束", message, false);
                return MiraiPluginTaskState.Canceled;
            }

            var search = SearchSong(message.Command);

            var procResult =
                new Func<ArcaeaSong?, Task<MiraiPluginTaskState>>(s => ProcSongGuessResult(ms, message, song, s));

            switch (search.Count)
            {
                case 0:
                    return await procResult(null);
                case 1:
                    return await procResult(search[0]);
                default:
                    ms.Send(GetSearchResult(search), message);
                    return MiraiPluginTaskState.ToBeContinued;
            }
        };
    }

    private bool StartGuess(ArcaeaSong song, MessageSenderProvider ms, Message message, long qq)
    {
        var senderId   = message.Sender!.Id;
        var senderName = message.Sender!.Name;
        var groupId    = message.GroupInfo!.Id;
        var now        = DateTime.Now;
        var res = Dialog.AddHandler(groupId,
            (sender, msg) => GenGuessDialogHandler(song, now, qq)(sender, msg));

        if (!res)
        {
            ms.Send("？", message);
            return false;
        }

        using var dbContext = new BotDbContext();

        if (dbContext.ArcaeaGuesses.Any(g => g.UId == senderId))
        {
            var g = dbContext.ArcaeaGuesses.First(g => g.UId == senderId);
            g.Name       =  senderName;
            g.TimesStart += 1;
            dbContext.Update(g);
        }
        else
        {
            dbContext.ArcaeaGuesses.Add(new ArcaeaGuess(senderId, senderName)
            {
                TimesStart = 1
            });
        }

        dbContext.SaveChanges();

        return true;
    }

    private void StartSongCoverGuess(Message message, MessageSenderProvider ms, long qq)
    {
        var songs = SongList.ToList();

        var song = songs.RandomTake();

        var cover = ResourceManager.GetCover(song.CoverFileName);

        var cw = cover.Width  / 3;
        var ch = cover.Height / 3;

        if (StartGuess(song, ms, message, qq))
        {
            ms.Send(new MessageChain(new MessageData[]
            {
                new PlainMessage("猜曲模式启动！"),
                ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);
        }
    }
}