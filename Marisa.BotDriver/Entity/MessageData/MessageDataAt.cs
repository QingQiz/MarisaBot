namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataAt : MessageData
{
    public long Target;
    public string Display;

    public MessageDataAt(long target, string display = "")
    {
        Type    = MessageDataType.At;
        Target  = target;
        Display = display;
    }
}