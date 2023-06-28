namespace Marisa.Plugin;

[MarisaPluginDoc("骂人")]
[MarisaPluginCommand("魔理沙骂")]
public class Dirty : MarisaPluginBase
{
    [MarisaPluginDoc("骂自己")]
    [MarisaPluginCommand(true, "我")]
    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
    private MarisaPluginTaskState Me(Message message)
    {
        var word = ConfigurationManager.Configuration.Dirty.RandomTake();

        message.Reply(word);

        return MarisaPluginTaskState.CompletedTask;
    }

    public static MarisaPluginTrigger.PluginTrigger Trigger = (message, _) => { return message.MessageChain!.Messages.Any(m => m.Type == MessageDataType.At); };

    [MarisaPluginDoc("@人：骂别人")]
    [MarisaPluginCommand(true, "")]
    [MarisaPluginTrigger(typeof(Dirty), nameof(Trigger))]
    private MarisaPluginTaskState You(Message message, long qq)
    {
        var word = ConfigurationManager.Configuration.Dirty.RandomTake();
        var at   = message.At().FirstOrDefault();

        if (at == qq || at == 642191352)
        {
            at = message.Sender!.Id;
        }

        message.Send(new MessageDataText(word), new MessageDataAt(at));

        return MarisaPluginTaskState.CompletedTask;
    }
}