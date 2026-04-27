namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataVoice : MessageData
{
    public string? VoiceId;
    public string? Url;
    public string? Path;
    public string? File;
    public string? Name;
    public string? FileSize;
    public string? FileUnique;
    public string? Base64;
    public long Length;

    public static MessageDataVoice FromFile(string file)
    {
        return new MessageDataVoice
        {
            File = file,
        };
    }

    public static MessageDataVoice FromBase64(string b64)
    {
        return new MessageDataVoice
        {
            Base64 = b64,
        };
    }

    public override MessageDataType Type => MessageDataType.Voice;
}
