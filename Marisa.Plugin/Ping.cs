namespace Marisa.Plugin;

[MarisaPluginDoc("确定bot是否存活，回复`pong`")]
[MarisaPluginCommand(true, ":ping")]
public class Ping : MarisaPluginBase
{
    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message message)
    {
        message.Reply("pong");

        return MarisaPluginTaskState.CompletedTask;
    }
}