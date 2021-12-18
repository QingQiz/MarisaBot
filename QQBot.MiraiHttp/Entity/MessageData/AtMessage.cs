namespace QQBot.MiraiHttp.Entity.MessageData;

public class AtMessage : MessageData
{
    public long Target;
    public string Display;

    public AtMessage(long target, string display = "")
    {
        Type    = MessageType.At;
        Target  = target;
        Display = display;
    }
}