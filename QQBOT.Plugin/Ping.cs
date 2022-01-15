using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(true, ":ping")]
public class Ping : MiraiPluginBase
{
    [MiraiPluginCommand]
    private MiraiPluginTaskState Handler(Message message, MessageSenderProvider sender)
    {
        sender.Reply(MessageChain.FromPlainText("ping"), message);

        return MiraiPluginTaskState.CompletedTask;
    }
}