namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataVoice : MessageData
{
    public readonly string? VoiceId = null;
    public readonly string? Url = null;
    public readonly string? Path = null;
    public string? Base64;
    public long Length;

    public static MessageDataVoice FromBase64(string b64)
    {
        return new MessageDataVoice
        {
            Base64 = b64,
        };
    }

    public override MessageDataType Type => MessageDataType.Voice;
}