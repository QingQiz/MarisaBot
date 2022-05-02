namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataVoice : MessageData
{
    public string? VoiceId = null;
    public string? Url = null;
    public string? Path = null;
    public string? Base64;
    public long Length;

    public static MessageDataVoice FromBase64(string b64)
    {
        return new MessageDataVoice
        {
            Base64 = b64,
            Type   = MessageDataType.Voice
        };
    }
}