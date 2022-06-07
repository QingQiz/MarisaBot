namespace Marisa.Plugin;

[MarisaPluginDoc("解决选择困难症的功能，参数为：A还是B还是C")]
[MarisaPluginCommand("请问")]
[MarisaPluginTrigger(typeof(Select), nameof(Trigger))]
public class Select : MarisaPluginBase
{
    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        if (message.Command.Any(char.IsPunctuation)) return false;
        if (message.Command.Any(char.IsWhiteSpace)) return false;
        if (message.Command.Contains("还是"))
        {
            return true;
        }

        return false;
    };

    [MarisaPluginCommand]
    private static MarisaPluginTaskState FriendMessageHandler(Message message)
    {
        var cmd = message.Command.Split("还是");
        message.Reply($"建议：{cmd.RandomTake()}");
        return MarisaPluginTaskState.CompletedTask;
    }
}