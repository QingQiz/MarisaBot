using System;
using System.Collections.Generic;

namespace QQBOT.Core.MiraiHttp.Entity
{
    public class MessageChain
    {

        public List<MessageData> Messages = new();

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
                        Messages.Add(new SourceMessage(m.id, m.time) {Type = t});
                        break;
                    case MessageType.Plain:
                        Messages.Add(new PlainMessage(m.text) {Type = t});
                        break;
                    default:
                        continue;
                }
            }
        }
    }

    public class MessageData
    {
        public MessageType Type;
    }

    public class SourceMessage : MessageData
    {
        public long Id;
        public long Time;

        public SourceMessage(long id, long time)
        {
            Id   = id;
            Time = time;
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

    public enum MessageType
    {
        Source, Quote, At, AtAll, Face, Plain, Image, FlashImage, Voice, Xml, Json, App, Poke, Dice, MusicShare, Forward, File, MiraiCode
    }
}