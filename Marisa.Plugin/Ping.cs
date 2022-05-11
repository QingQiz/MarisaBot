namespace Marisa.Plugin;

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