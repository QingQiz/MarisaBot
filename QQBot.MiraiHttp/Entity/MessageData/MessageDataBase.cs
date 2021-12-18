namespace QQBot.MiraiHttp.Entity.MessageData;

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
            case MessageType.Voice:
            {
                var d = this as VoiceMessage;

                return new
                {
                    voiceId = d!.VoiceId,
                    url     = d!.Url,
                    path    = d!.Path,
                    base64  = d!.Base64,
                    type    = "Voice"
                };
            }
            default:
                throw new NotImplementedException($"Converter for type {Type}Message is not implemented");
        }
    }
}