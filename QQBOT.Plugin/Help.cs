using System.Configuration;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand(true, "help", "帮助")]
public class Help : MiraiPluginBase
{
    [MiraiPluginCommand]
    private MiraiPluginTaskState Handler(Message message, MessageSenderProvider sender)
    {
        sender.Reply(ImageMessage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"], "help.png")), message, false);

        return MiraiPluginTaskState.CompletedTask;
    }
}