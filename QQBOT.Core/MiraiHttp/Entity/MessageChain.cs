using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QQBOT.Core.MiraiHttp.Entity
{
    public class MessageChain
    {

        public List<MessageData> Messages = new();

        public MessageChain(IEnumerable<MessageData> messages)
        {
            Messages = messages.ToList();
        }

        public static MessageChain FromPlainText(string text)
        {
            return new MessageChain(new[]
            {
                new PlainMessage(text)
            });
        }

        public static MessageChain FromBase64(string b64)
        {
            return new MessageChain(new[]
            {
                ImageMessage.FromBase64(b64)
            });
        }

        public MessageChain(IEnumerable<dynamic> data)
        {
            foreach (var m in data)
            {
                Enum.TryParse(typeof(MessageType), m.type, true, out object t_);
                
                if (t_ == null) continue;

                var t = (MessageType)t_;

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
                    default:
                        continue;
                }
            }
        }

        public string PlainText => string.Join(' ',
            Messages.Where(m => m.Type == MessageType.Plain).Select(m => (m as PlainMessage)?.Text));
    }

    public class MessageData
    {
        public MessageType Type;

        public object ConvertToObject()
        {
            switch (Type)
            {
                case MessageType.Plain:
                    return new
                    {
                        type = "Plain",
                        text = (this as PlainMessage)!.Text
                    };
                case MessageType.At:
                    return new
                    {
                        type    = "At",
                        target  = (this as AtMessage)!.Target,
                        display = (this as AtMessage)!.Display
                    };
                case MessageType.Image:
                    return new
                    {
                        type   = "Image",
                        base64 = (this as ImageMessage)!.Base64
                    };
                default:
                    throw new NotImplementedException($"Converter for type {Type}Message is not implemented");
            }
        }
    }

    public class SourceMessage : MessageData
    {
        public long Id;
        public long Time;

        public SourceMessage(long id, long time)
        {
            Id   = id;
            Time = time;
            Type = MessageType.Source;
        }
    }

    public class QuoteMessage : MessageData
    {
        public long Id;
        public long GroupId;
        public long SenderId;
        public long TargetId;

        public MessageChain Origin;
    }

    public class AtMessage : MessageData
    {
        public long Target;
        public string Display;

        public AtMessage(long target, string display)
        {
            Type    = MessageType.At;
            Target  = target;
            Display = display;
        }
    }

    public class FaceMessage : MessageData
    {
        public long FaceId;
        public string Name;
    }

    public class PlainMessage : MessageData
    {
        public PlainMessage(string text)
        {
            Type = MessageType.Plain;
            Text = text;
        }
        public string Text;
    }

    public class ImageMessage : MessageData
    {
        public string ImageId;
        public string Url;
        public string Path;
        public string Base64;

        public static ImageMessage FromBase64(string base64)
        {
            return new ImageMessage
            {
                Type   = MessageType.Image,
                Base64 = base64
            };
        }
    }

    public class Voice : MessageData
    {
        public string VoiceId;
        public string ImageId;
        public string Url;
        public string Path;
        public string Base64;
        public long Length;
    }

    public class PokeMessage
    {
        public string Name;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageType
    {
        Source, Quote, At, AtAll, Face, Plain, Image, FlashImage, Voice, Xml, Json, App, Poke, Dice, MusicShare, Forward, File, MiraiCode
    }
}