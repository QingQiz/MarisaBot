using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(true, "help", "帮助")]
public class Help : MiraiPluginBase
{
    [MiraiPluginCommand]
    private MiraiPluginTaskState Handler(Message message, MessageSenderProvider sender)
    {
        const string help = "帮助见 https://github.com/QingQiz/QQBOT#%E5%8A%9F%E8%83%BD";
        sender.Reply(MessageChain.FromPlainText(help), message);

        return MiraiPluginTaskState.CompletedTask;
    }
}