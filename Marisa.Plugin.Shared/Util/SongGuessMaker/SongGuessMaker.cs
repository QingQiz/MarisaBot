using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Marisa.Plugin.Shared.Util.SongDb;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Util.SongGuessMaker;

public class SongGuessMaker<TSong, TSongGuess>(SongDb<TSong> songDb, string guessDbSetName)
    where TSong : Song
    where TSongGuess : SongGuess, new()
{
    private async Task<MarisaPluginTaskState> ProcSongGuessResult(Message message, TSong song, TSong? guess)
    {
        var dbContext  = new BotDbContext();
        var senderId   = message.Sender.Id;
        var senderName = message.Sender.Name;

        var db   = (DbSet<TSongGuess>)dbContext.GetType().GetProperty(guessDbSetName)!.GetValue(dbContext, null)!;
        var @new = db.Any(u => u.UId == senderId);
        var u = @new
            ? db.First(u => u.UId == senderId)
            : new SongGuess(senderId, senderName).CastTo<TSongGuess>();

        // 未知的歌，不算
        if (guess == null)
        {
            message.Reply("没找到你说的这首歌");
            return MarisaPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            u.TimesCorrect++;
            u.Name = senderName;
            db.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            message.Reply(
                new MessageDataText($"你猜对了！正确答案：{song.Title}"),
                MessageDataImage.FromBase64(song.GetImage())
            );

            return MarisaPluginTaskState.CompletedTask;
        }

        // 猜错了
        u.TimesWrong++;
        u.Name = senderName;
        db.InsertOrUpdate(u);
        await dbContext.SaveChangesAsync();

        message.Reply("不对不对！");
        return MarisaPluginTaskState.ToBeContinued;
    }

    private Dialog.Dialog.MessageHandler GenGuessDialogHandler(TSong song, DateTime startTime, long qq)
    {
        return async message =>
        {
            switch (message.Command.Span)
            {
                case "结束猜曲" or "答案":
                {
                    message.Reply(
                        new MessageDataText($"猜曲结束，正确答案：{song.Title}"),
                        MessageDataImage.FromBase64(song.GetImage()),
                        new MessageDataText(
                            $"当前歌在录的别名有：{string.Join(", ", songDb.GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                    );
                    return MarisaPluginTaskState.CompletedTask;
                }
                case "来点提示":
                {
                    MessageChain? hint = null;
                    switch (new Random().Next(3))
                    {
                        case 0:
                            hint = MessageChain.FromText($"作曲家是：{song.Artist}");
                            break;
                        case 1:
                            hint = MessageChain.FromText($"是个{song.MaxLevel()}");
                            break;
                        case 2:
                        {
                            var cover = song.GetCover();
                            cover.Mutate(i => i.RandomCut(cover.Width / 3, cover.Height / 3));

                            hint = new MessageChain(
                                new MessageDataText("封面裁剪："),
                                MessageDataImage.FromBase64(cover.ToB64())
                            );
                            break;
                        }
                    }

                    message.Reply(hint!, false);

                    return MarisaPluginTaskState.ToBeContinued;
                }
            }

            if (!message.IsAt(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MarisaPluginTaskState.NoResponse;

                // time out
                message.Reply("猜曲已结束", false);
                return MarisaPluginTaskState.Canceled;
            }

            var search = songDb.SearchSong(message.Command).DistinctBy(s => s.Title).ToList();

            var procResult =
                new Func<TSong?, Task<MarisaPluginTaskState>>(s => ProcSongGuessResult(message, song, s));

            switch (search.Count)
            {
                case 0:
                    return await procResult(null);
                case 1:
                    return await procResult(search[0]);
                default:
                    message.Reply(songDb.GetSearchResult(search));
                    return MarisaPluginTaskState.ToBeContinued;
            }
        };
    }

    private bool StartGuess(TSong song, Message message, long qq)
    {
        var senderId   = message.Sender.Id;
        var senderName = message.Sender.Name;
        var groupId    = message.GroupInfo!.Id;
        var now        = DateTime.Now;
        var res        = songDb.MessageHandlerAdder(groupId, null, msg => GenGuessDialogHandler(song, now, qq)(msg));

        if (!res)
        {
            message.Reply("？");
            return false;
        }

        using var dbContext = new BotDbContext();

        var db = (DbSet<TSongGuess>)dbContext.GetType().GetProperty(guessDbSetName)!.GetValue(dbContext, null)!;
        if (db.Any(g => g.UId == senderId))
        {
            var g = db.First(g => g.UId == senderId);
            g.Name       =  senderName;
            g.TimesStart += 1;
            dbContext.Update(g);
        }
        else
        {
            db.Add(new SongGuess(senderId, senderName)
            {
                TimesStart = 1
            }.CastTo<TSongGuess>());
        }

        dbContext.SaveChanges();

        return true;
    }

    public void StartSongCoverGuess(
        Message message, long qq, int widthDiv,
        Func<TSong, bool>? filter)
    {
        var songs = songDb.SongList.Where(s => filter?.Invoke(s) ?? true).ToList();

        if (songs.Count == 0)
        {
            message.Reply("None");
            return;
        }

        var song = songs.RandomTake();

        var cover = song.GetCover();

        var cw = cover.Width / widthDiv;
        var ch = cover.Height / widthDiv;

        cover.Mutate(i => i.RandomCut(cw, ch));

        if (StartGuess(song, message, qq))
        {
            message.Reply(
                new MessageDataText("猜曲模式启动！"),
                MessageDataImage.FromBase64(cover.ToB64()),
                new MessageDataText("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            );
        }
    }
}