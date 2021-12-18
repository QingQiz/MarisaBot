namespace QQBot.MiraiHttp.Entity.MessageData;

public class PlainMessage : MessageData
{
    public PlainMessage(string text)
    {
        Type = MessageType.Plain;
        Text = text;
    }

    public string Text;
}