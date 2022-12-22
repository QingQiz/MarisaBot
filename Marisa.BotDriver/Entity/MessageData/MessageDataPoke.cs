// ReSharper disable MemberCanBePrivate.Global
namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataNudge : MessageData
{
    public readonly long Target;
    public readonly long FromId;

    public MessageDataNudge(long target, long fromId)
    {
        Target    = target;
        FromId    = fromId;
    }

    public override MessageDataType Type => MessageDataType.Nudge;
}