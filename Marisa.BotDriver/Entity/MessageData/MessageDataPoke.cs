// ReSharper disable MemberCanBePrivate.Global
namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataNudge : MessageData
{
    public readonly long Target;
    public readonly long FromId;
    public long SubjectId;
    public string Action;
    public string Suffix;

    public MessageDataNudge(long target, long fromId, long subjectId, string action, string suffix)
    {
        Target    = target;
        FromId    = fromId;
        SubjectId = subjectId;
        Action    = action;
        Suffix    = suffix;
    }

    public override MessageDataType Type => MessageDataType.Nudge;
}