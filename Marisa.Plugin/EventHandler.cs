namespace Marisa.Plugin;

[MarisaPlugin]
[MarisaPluginTrigger(typeof(EventHandler), nameof(Trigger))]
public class EventHandler : MarisaPluginBase
{
    public static MarisaPluginTrigger.PluginTrigger Trigger => (message, _) =>
    {
        return message.MessageChain!.Messages.Any(m => m.Type is MessageDataType.Nudge);
    };

    [MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
    private MarisaPluginTaskState Handler(Message message, long qq)
    {
        var msg = message.MessageChain!.Messages.First(m => m.Type != MessageDataType.Id);

        switch (msg.Type)
        {
            // 戳一戳
            case MessageDataType.Nudge:
            {
                var m = (msg as MessageDataNudge)!;

                if (m.Target != qq) break;

                var word = ConfigurationManager.Configuration.Dirty.RandomTake();

                if (ConfigurationManager.Configuration.Commander.Contains(m.FromId)) word = "别戳啦！";

                if (message.GroupInfo != null)
                {
                    message.Reply(
                        new MessageDataAt(m.FromId),
                        new MessageDataText(" "),
                        new MessageDataText(word)
                    );
                }
                else
                {
                    message.Reply(word);
                }

                break;
            }
            case MessageDataType.Quote:
            case MessageDataType.At:
            case MessageDataType.AtAll:
            case MessageDataType.Face:
            case MessageDataType.Text:
            case MessageDataType.Image:
            case MessageDataType.FlashImage:
            case MessageDataType.Voice:
            case MessageDataType.Xml:
            case MessageDataType.Json:
            case MessageDataType.App:
            case MessageDataType.Dice:
            case MessageDataType.MusicShare:
            case MessageDataType.Forward:
            case MessageDataType.File:
            case MessageDataType.MiraiCode:
            case MessageDataType.Id:
            case MessageDataType.Unknown:
            default:
                throw new ArgumentOutOfRangeException();
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}