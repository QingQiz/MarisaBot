using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared;

namespace Marisa.Plugin;

[MarisaPluginCommand]
[MarisaPlugin(PluginPriority.BlackList)]
public class BlackList : MarisaPluginBase
{
    private static readonly HashSet<long> Cache = new();

    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message message)
    {
        var u = message.Sender!.Id;

        var db = new BotDbContext();

        if (!db.BlackLists.Any(b => b.UId == u)) return MarisaPluginTaskState.NoResponse;

        if (Cache.Contains(u))
        {
        }
        else
        {
            message.Reply("这。。", false);
            Cache.Add(u);
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand(":ban")]
    private static MarisaPluginTaskState Ban(Message message)
    {
        const long admin = 642191352L;

        if (message.Sender!.Id != admin)
        {
            message.Reply("你没有资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(message.Command))
        {
            var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);

            if (at == null)
            {
                message.Reply("错误的命令格式");
            }
            else
            {
                var db = new BotDbContext();
                var qq = (at as MessageDataAt)!.Target;
                db.BlackLists.Add(new EntityFrameworkCore.Entity.BlackList(qq));
                db.SaveChanges();
                message.Reply("好了");
            }
        }
        else
        {
            if (long.TryParse(message.Command.Trim(), out var qq))
            {
                var db = new BotDbContext();
                db.BlackLists.Add(new EntityFrameworkCore.Entity.BlackList(qq));
                db.SaveChanges();
                message.Reply("好了");
            }
            else
            {
                message.Reply("错误的命令格式");
            }
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("list", "ls")]
    [MarisaPluginSubCommand(nameof(Ban))]
    private static MarisaPluginTaskState BanList(Message message)
    {
        var db = new BotDbContext();
        message.Reply(string.Join("\n", db.BlackLists.Select(b => b.UId)));
        return MarisaPluginTaskState.CompletedTask;
    }
}