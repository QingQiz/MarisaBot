using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(MiraiMessageType.GroupMessage, true, "help", "帮助")]
[MiraiPluginCommand(MiraiMessageType.FriendMessage, true, ":help", ":h", "帮助")]
[MiraiPluginTrigger(typeof(MiraiPluginTrigger), nameof(MiraiPluginTrigger.AtBotTrigger), MiraiMessageType.GroupMessage)]
public class Help : MiraiPluginBase
{
    [MiraiPluginCommand]
    private MiraiPluginTaskState Handler(Message message, MessageSenderProvider sender)
    {
        const string help = "帮助见 https://github.com/QingQiz/QQBOT#%E5%8A%9F%E8%83%BD";
        sender.SendByRecv(MessageChain.FromPlainText(help), message);

        return MiraiPluginTaskState.CompletedTask;
    }
}