using Marisa.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.BlackList)]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
public class BlackList : MarisaPluginBase
{
    private static readonly HashSet<long> Cache = new();
    private static bool _cacheInitialized;

    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
    private static async Task<MarisaPluginTaskState> Handler(Message message)
    {
        var u = message.Sender.Id;

        if (!_cacheInitialized)
        {
            _cacheInitialized = true;

            var db = new BotDbContext();
            await db.BlackLists.ForEachAsync(b => Cache.Add(b.UId));
        }

        if (Cache.Contains(u)) return MarisaPluginTaskState.CompletedTask;

        if (message.GroupInfo == null)
        {
            return MarisaPluginTaskState.NoResponse; // 插件不处理这条消息，等于不ban
        }

        {
            var db      = new BotDbContext();
            var filters = db.CommandFilters.Where(x => x.GroupId == message.GroupInfo.Id);

            foreach (var f in filters)
            {
                if (!string.IsNullOrWhiteSpace(f.Prefix))
                {
                    if (message.Command.StartsWith(f.Prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // 阻断
                        return MarisaPluginTaskState.CompletedTask;
                    }
                }

                if (string.IsNullOrWhiteSpace(f.Type)) continue;

                var t = (MessageDataType)Enum.Parse(typeof(MessageDataType), f.Type);

                if (message.MessageChain!.Messages.Any(x => x.Type == t))
                {
                    // 阻断
                    return MarisaPluginTaskState.CompletedTask;
                }
            }
        }

        return MarisaPluginTaskState.NoResponse; // 插件不处理这条消息，等于不ban
    }

    [MarisaPluginCommand(":ban")]
    private static MarisaPluginTaskState Ban(Message message)
    {
        if (!ConfigurationManager.Configuration.Commander.Contains(message.Sender.Id))
        {
            message.Reply("你没有资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!TryGetId(message, out var qq))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        using var db = new BotDbContext();

        Cache.Add(qq);
        db.BlackLists.Add(new EntityFrameworkCore.Entity.BlackList(qq));
        db.SaveChanges();
        message.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand(":unban")]
    private static MarisaPluginTaskState UnBan(Message message)
    {
        if (!ConfigurationManager.Configuration.Commander.Contains(message.Sender.Id))
        {
            message.Reply("你没有资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!TryGetId(message, out var qq))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        using var db = new BotDbContext();

        var item = db.BlackLists.FirstOrDefault(b => b.UId == qq);

        if (item != null)
        {
            Cache.Remove(qq);
            db.BlackLists.Remove(item);
            db.SaveChanges();
            message.Reply("好了");
        }
        else
        {
            message.Reply("这个人不在黑名单里");
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

    private static bool TryGetId(Message message, out long qq)
    {
        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);

        if (at != null) qq = (at as MessageDataAt)!.Target;
        else if (long.TryParse(message.Command.Trim(), out qq))
        {
        }
        else
        {
            message.Reply("错误的命令格式");
            return false;
        }

        return true;
    }
}