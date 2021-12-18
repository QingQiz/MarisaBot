namespace QQBot.MiraiHttp.Entity.MessageData;

public class VoiceMessage : MessageData
{
    public string? VoiceId = null;
    public string? Url = null;
    public string? Path = null;
    public string? Base64;
    public long Length;

    public static VoiceMessage FromBase64(string b64)
    {
        return new VoiceMessage
        {
            Base64 = b64,
            Type   = MessageType.Voice
        };
    }
}