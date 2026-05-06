using Marisa.Database;
using Marisa.Database.Entity;
using BlackListEntity = Marisa.Database.Entity.BlackList;

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

            using var realm = BotDbContext.OpenRealm();
            foreach (var b in realm.All<BlackListEntity>())
            {
                Cache.Add(b.UId);
            }
        }

        if (Cache.Contains(u)) return MarisaPluginTaskState.CompletedTask;

        return MarisaPluginTaskState.NoResponse; // 插件不处理这条消息，等于不ban
    }

    [MarisaPluginCommand(":ban")]
    private static MarisaPluginTaskState Ban(Message message)
    {
        var commanders = ConfigurationManager.Configuration.Commander;

        if (!commanders.Contains(message.Sender.Id))
        {
            message.Reply("你没有资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!TryGetId(message, out var qq))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        using var realm = BotDbContext.OpenRealm();

        Cache.Add(qq);
        realm.Write(() => realm.Add(new BlackListEntity(qq)
        {
            Id = BotDbContext.NextId<BlackListEntity>(realm)
        }));
        message.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand(":unban")]
    private static MarisaPluginTaskState UnBan(Message message)
    {
        var commanders = ConfigurationManager.Configuration.Commander;

        if (!commanders.Contains(message.Sender.Id))
        {
            message.Reply("你没有资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!TryGetId(message, out var qq))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        using var realm = BotDbContext.OpenRealm();

        var item = realm.All<BlackListEntity>().FirstOrDefault(b => b.UId == qq);

        if (item != null)
        {
            Cache.Remove(qq);
            realm.Write(() => realm.Remove(item));
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
        using var realm = BotDbContext.OpenRealm();
        message.Reply(string.Join("\n", realm.All<BlackListEntity>().Select(b => b.UId)));
        return MarisaPluginTaskState.CompletedTask;
    }

    private static bool TryGetId(Message message, out long qq)
    {
        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);

        if (at != null) qq = (at as MessageDataAt)!.Target;
        else if (long.TryParse(message.Command.Trim().Span, out qq))
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
