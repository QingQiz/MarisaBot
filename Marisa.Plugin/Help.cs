using System.Configuration;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;

namespace Marisa.Plugin;

[MarisaPluginCommand(true, "help", "帮助")]
public class Help : MarisaPluginBase
{
    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message message)
    {
        message.Reply(MessageDataImage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"], "help.png")), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}