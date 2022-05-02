using Flurl;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;

namespace Marisa.Plugin;

[MarisaPluginCommand("生成")]
public class ImGenerator : MarisaPluginBase
{
    private static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) => message.Command.Contains('/');

    [MarisaPluginTrigger(typeof(ImGenerator), nameof(Trigger))]
    private static MarisaPluginTaskState Handler(Message msg)
    {
        var message = msg.Command.Split('/');

        if (message.Length < 2)
        {
            msg.Reply("NoResponse");
            return MarisaPluginTaskState.CompletedTask;
        }

        var url = "http://112.124.22.246:4000/api/v1/gen"
            .SetQueryParam("top", message[0])
            .SetQueryParam("bottom", string.Join('/', message[1..]));
        
        msg.Reply(MessageDataImage.FromUrl(url), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}