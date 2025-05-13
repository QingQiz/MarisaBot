using Marisa.BotDriver.Entity.MessageData;

namespace Marisa.BotDriver.Entity.Message;

public class MessageBuilder(Message message)
{
    private readonly List<MessageData.MessageData> _data = [];

    public MessageBuilder Text(string text)
    {
        _data.Add(new MessageDataText(text));
        return this;
    }

    public MessageBuilder ImgB64(string b64)
    {
        _data.Add(MessageDataImage.FromBase64(b64));
        return this;
    }

    public MessageBuilder ImgFile(string path)
    {
        _data.Add(MessageDataImage.FromPath(path));
        return this;
    }

    public MessageBuilder ImageUrl(string url)
    {
        _data.Add(MessageDataImage.FromUrl(url));
        return this;
    }

    public MessageBuilder At(long qq)
    {
        _data.Add(new MessageDataAt(qq));
        return this;
    }

    public void Reply(bool quote = true)
    {
        message.Reply(new MessageChain(_data), quote);
    }
}