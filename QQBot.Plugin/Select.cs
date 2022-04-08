using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin;

[MiraiPluginCommand("请问")]
[MiraiPluginTrigger(typeof(Select), nameof(Trigger))]
public class Select : MiraiPluginBase
{
    public static MiraiPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        if (message.Command.Any(char.IsPunctuation)) return false;
        if (message.Command.Any(char.IsWhiteSpace)) return false;
        if (message.Command.Contains("还是"))
        {
            return true;
        }
        return false;
    };

    [MiraiPluginCommand]
    private static MiraiPluginTaskState FriendMessageHandler(MessageSenderProvider ms, Message message)
    {
        var cmd = message.Command.Split("还是");
        ms.Reply($"建议：{cmd.RandomTake()}", message);
        return MiraiPluginTaskState.CompletedTask;
    }
}