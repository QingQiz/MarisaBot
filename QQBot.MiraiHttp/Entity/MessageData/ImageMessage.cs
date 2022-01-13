namespace QQBot.MiraiHttp.Entity.MessageData;

public class ImageMessage : MessageData
{
    public string? ImageId;
    public string? Url;
    public string? Path;
    public string? Base64;

    public static ImageMessage FromBase64(string base64)
    {
        return new ImageMessage
        {
            Type   = MessageType.Image,
            Base64 = base64
        };
    }

    public static ImageMessage FromUrl(string url)
    {
        return new ImageMessage
        {
            Type = MessageType.Image,
            Url  = url
        };
    }
}