namespace Marisa.BotDriver.Entity.MessageData;

public class MessageData
{
    public MessageDataType Type;

    public object ToObject()
    {
        switch (Type)
        {
            case MessageDataType.Text:
                return new
                {
                    type = "Plain",
                    text = (this as MessageDataText)!.Text
                };
            case MessageDataType.At:
            {
                var m = (this as MessageDataAt)!;
                return new
                {
                    type    = "At",
                    target  = m.Target,
                    display = m.Display
                };
            }
            case MessageDataType.Image:
            {
                var m = this as MessageDataImage;
                
                return new
                {
                    type   = "Image",
                    base64 = m?.Base64,
                    url    = m?.Url,
                    path   = m?.Path
                };
            }
            case MessageDataType.Voice:
            {
                var d = this as MessageDataVoice;

                return new
                {
                    voiceId = d!.VoiceId,
                    url     = d!.Url,
                    path    = d!.Path,
                    base64  = d!.Base64,
                    type    = "Voice"
                };
            }
            case MessageDataType.Id:
            case MessageDataType.Quote:
            case MessageDataType.AtAll:
            case MessageDataType.Face:
            case MessageDataType.FlashImage:
            case MessageDataType.Xml:
            case MessageDataType.Json:
            case MessageDataType.App:
            case MessageDataType.Nudge:
            case MessageDataType.Dice:
            case MessageDataType.MusicShare:
            case MessageDataType.Forward:
            case MessageDataType.File:
            case MessageDataType.MiraiCode:
            case MessageDataType.Unknown:
            default:
                throw new NotImplementedException($"Converter for {Type} is not implemented");
        }
    }
}