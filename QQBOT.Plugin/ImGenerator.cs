using Flurl;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

[MiraiPluginCommand("生成")]
public class ImGenerator : MiraiPluginBase
{
    private static MiraiPluginTrigger.PluginTrigger Trigger => (message, _) => message.Command.Contains('/');

    [MiraiPluginTrigger(typeof(ImGenerator), nameof(Trigger))]
    private static MiraiPluginTaskState Handler(Message msg, MessageSenderProvider ms)
    {
        var message = msg.Command.Split('/');

        if (message.Length < 2)
        {
            ms.Reply("NoResponse", msg);
            return MiraiPluginTaskState.CompletedTask;
        }

        var url = $"http://112.124.22.246:4000/api/v1/gen"
            .SetQueryParam("top", message[0])
            .SetQueryParam("bottom", string.Join('/', message[1..]));
        
        ms.Reply(ImageMessage.FromUrl(url), msg, false);

        return MiraiPluginTaskState.CompletedTask;
    }
}