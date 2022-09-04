namespace Marisa.Plugin;

[MarisaPluginDoc("骂你")]
[MarisaPluginCommand("魔理沙骂我")]
public class Dirty : MarisaPluginBase
{
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private MarisaPluginTaskState Handler(Message message)
    {
        var word = ConfigurationManager.Configuration.Dirty.RandomTake();

        message.Reply(word);

        return MarisaPluginTaskState.CompletedTask;
    }
}