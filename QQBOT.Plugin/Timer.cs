using QQBot.EntityFrameworkCore;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(MiraiMessageType.FriendMessage)]
public class Timer : MiraiPluginBase
{
    [MiraiPluginCommand(":ts")]
    private MiraiPluginTaskState TimerStart(Message message, MessageSenderProvider ms)
    {
        using var dbContext = new BotDbContext();
        var       uid       = message.Sender!.Id;

        if (dbContext.Timers.Any(t => t.Uid == uid && t.Name == message.Command && t.TimeEnd == null))
        {
            ms.SendByRecv(
                MessageChain.FromPlainText($"Timer `{message.Command}` already started"), message);
        }
        else
        {
            var time = DateTime.Now;

            dbContext.Timers.Add(new EntityFrameworkCore.Entity.Plugin.Timer
            {
                TimeBegin = time,
                TimeEnd   = null,
                Uid       = uid,
                Name      = message.Command
            });

            dbContext.SaveChanges();
            ms.SendByRecv(MessageChain.FromPlainText(
                $"Timer `{message.Command}` started: {time:yyyy-MM-dd hh:mm:ss fff}"), message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    [MiraiPluginCommand(":te")]
    private MiraiPluginTaskState TimerEnd(Message message, MessageSenderProvider ms)
    {
        using var dbContext = new BotDbContext();
        var       last      = message.Command;
        var       uid       = message.Sender!.Id;

        var res = dbContext.Timers.Where(t => t.Uid == uid && t.TimeEnd == null && t.Name == last);

        if (res.Any())
        {
            var update = res.First();
            var time   = DateTime.Now;
            update.TimeEnd = time;
            dbContext.Update(update);

            dbContext.SaveChanges();
            ms.SendByRecv(MessageChain.FromPlainText(
                $"Timer `{last}` ended, duration: {time - update.TimeBegin:dd\\.hh\\:mm\\:ss}"), message);
        }
        else
        {
            ms.SendByRecv(MessageChain.FromPlainText($"Timer `{last}` already ended"), message);
        }

        return MiraiPluginTaskState.CompletedTask;
    }
}