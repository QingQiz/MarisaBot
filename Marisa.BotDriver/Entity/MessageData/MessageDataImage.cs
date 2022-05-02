namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataImage : MessageData
{
    public string? ImageId;
    public string? Url;
    public string? Path;
    public string? Base64;

    public static MessageDataImage FromBase64(string base64)
    {
        return new MessageDataImage
        {
            Type   = MessageDataType.Image,
            Base64 = base64
        };
    }

    public static MessageDataImage FromUrl(string url)
    {
        return new MessageDataImage
        {
            Type = MessageDataType.Image,
            Url  = url
        };
    }

    public static MessageDataImage FromPath(string path)
    {
        return new MessageDataImage
        {
            Type = MessageDataType.Image,
            Path =  path,
        };
    }
}