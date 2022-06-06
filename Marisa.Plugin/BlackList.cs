using Marisa.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPluginCommand]
[MarisaPlugin(PluginPriority.BlackList)]
public class BlackList : MarisaPluginBase
{
    private static readonly HashSet<long> Cache = new();
    private static bool _cacheInitialized = false;

    [MarisaPluginCommand]
    private static async Task<MarisaPluginTaskState> Handler(Message message)
    {
        var u = message.Sender!.Id;

        if (!_cacheInitialized)
        {
            _cacheInitialized = true;
           
            var db = new BotDbContext();
            await db.BlackLists.ForEachAsync(b => Cache.Add(b.UId));
        }

        return Cache.Contains(u)
            ? MarisaPluginTaskState.CompletedTask // 插件处理了这条消息，并阻断了消息传播，等于ban了
            : MarisaPluginTaskState.NoResponse;   // 插件不处理这条消息，等于不ban
    }

    [MarisaPluginCommand(":ban")]
    private static MarisaPluginTaskState Ban(Message message)
    {
        if (!ConfigurationManager.Configuration.Commander.Contains(message.Sender!.Id))
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
                Cache.Add(qq);
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