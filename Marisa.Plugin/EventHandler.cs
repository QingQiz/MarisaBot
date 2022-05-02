using System.Configuration;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Attributes;
using Marisa.BotDriver.Plugin.Trigger;

namespace Marisa.Plugin;

[MarisaPlugin]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.AlwaysTrueTrigger))]
public class EventHandler : MarisaPluginBase
{
    private readonly List<string> _dirtyWords;
    private static readonly string ResourcePath = ConfigurationManager.AppSettings["Dirty.ResourcePath"]!;

    public EventHandler()
    {
        _dirtyWords = File.ReadAllText(ResourcePath + "/dirty.txt")
            .Trim()
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private MarisaPluginTaskState Handler(Message message, long qq)
    {
        var msg = message.MessageChain!.Messages.First(m => m.Type != MessageDataType.Id);

        switch (msg.Type)
        {
            case MessageDataType.Nudge:
            {
                var m = (msg as MessageDataNudge)!;

                if (m.Target != qq) break;

                var word = _dirtyWords[new Random().Next(_dirtyWords.Count)];

                if (m.FromId == 642191352) word = "别戳啦！";

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