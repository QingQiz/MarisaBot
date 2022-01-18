using QQBot.MiraiHttp.Entity.MessageData;

namespace QQBot.MiraiHttp.Entity
{
    public class MessageChain
    {
        public readonly List<MessageData.MessageData> Messages = new();

        public MessageChain(IEnumerable<MessageData.MessageData> messages)
        {
            Messages = messages.ToList();
        }

        public bool EnableReference = true;

        public bool CanReference => EnableReference && Messages.All(m => m.Type != MessageType.Voice);

        public static MessageChain FromPlainText(string text)
        {
            return new MessageChain(new[]
            {
                new PlainMessage(text)
            });
        }

        public static MessageChain FromImageB64(string b64)
        {
            return new MessageChain(new[]
            {
                ImageMessage.FromBase64(b64)
            });
        }

        public static MessageChain FromVoiceB64(string b64)
        {
            return new MessageChain(new[]
            {
                VoiceMessage.FromBase64(b64),
            });
        }

        public MessageChain(IEnumerable<dynamic> data)
        {
            foreach (var m in data)
            {
                Enum.TryParse(typeof(MessageType), m.type, true, out object type);

                var t = (MessageType)type;

                switch (t)
                {
                    case MessageType.Source:
                        Messages.Add(new SourceMessage(m.id, m.time));
                        break;
                    case MessageType.Plain:
                        Messages.Add(new PlainMessage(m.text));
                        break;
                    case MessageType.At:
                        Messages.Add(new AtMessage(m.target, m.display));
                        break;
                    case MessageType.Quote:
                    case MessageType.AtAll:
                    case MessageType.Face:
                    case MessageType.Image:
                    case MessageType.FlashImage:
                    case MessageType.Voice:
                    case MessageType.Xml:
                    case MessageType.Json:
                    case MessageType.App:
                    case MessageType.Poke:
                    case MessageType.Dice:
                    case MessageType.MusicShare:
                    case MessageType.Forward:
                    case MessageType.File:
                    case MessageType.MiraiCode:
                    default:
                        Messages.Add(new NotImplementedMessage());
                        continue;
                }
            }
        }

        public string PlainText => string.Join(' ',
            Messages.Where(m => m.Type == MessageType.Plain).Select(m => (m as PlainMessage)?.Text));
    }
}