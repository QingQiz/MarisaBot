using System;
using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Util;
using QQBOT.EntityFrameworkCore;

namespace QQBOT.Core.Plugin
{
    public class Timer : PluginBase
    {
        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = message.MessageChain!.PlainText;

            await using var dbContext = new BotDbContext();
            const string    command1 = ":ts";
            const string    command2 = ":te";
            var             uid       = message.Sender!.Id;

            switch (mc)
            {
                case command1:
                {
                    var last = mc.TrimStart(command1).Trim();

                    if (dbContext.Timers.Any(t => t.Uid == uid && t.Name == last && t.TimeEnd == null))
                    {
                        await session.SendFriendMessage(
                            new Message(MessageChain.FromPlainText($"Timer `{last}` already started")), uid);
                    }
                    else
                    {
                        var time = DateTime.Now;

                        dbContext.Timers.Add(new EntityFrameworkCore.Entity.Plugin.Timer
                        {
                            TimeBegin = time,
                            TimeEnd   = null,
                            Uid       = uid,
                            Name      = last
                        });

                        await dbContext.SaveChangesAsync();
                        await session.SendFriendMessage(
                            new Message(
                                MessageChain.FromPlainText($"Timer `{last}` started: {time:yyyy-MM-dd hh:mm:ss fff}")),
                            uid);
                    }
                    return PluginTaskState.CompletedTask;
                }
                case command2:
                {
                    var last = mc.TrimStart(command2).Trim();

                    var res = dbContext.Timers.Where(t => t.Uid == uid && t.TimeEnd == null && t.Name == last);

                    if (res.Any())
                    {
                        var update = res.First();
                        var time   = DateTime.Now;
                        update.TimeEnd = time;
                        dbContext.Update(update);

                        await dbContext.SaveChangesAsync();
                        await session.SendFriendMessage(
                            new Message(
                                MessageChain.FromPlainText(
                                    $"Timer `{last}` ended, duration: {time - update.TimeBegin:dd\\.hh\\:mm\\:ss}")),
                            uid);
                    }
                    else
                    {
                        await session.SendFriendMessage(
                            new Message(MessageChain.FromPlainText($"Timer `{last}` already ended")), uid);
                    }

                    return PluginTaskState.CompletedTask;
                }
                default:
                    return PluginTaskState.NoResponse;
            }
        }
    }
}