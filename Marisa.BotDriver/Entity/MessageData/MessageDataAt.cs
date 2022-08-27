namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataAt : MessageData
{
    public readonly long Target;
    public readonly string Display;

    public MessageDataAt(long target, string display = "")
    {
        Target  = target;
        Display = display;
    }

    public override MessageDataType Type => MessageDataType.At;
}