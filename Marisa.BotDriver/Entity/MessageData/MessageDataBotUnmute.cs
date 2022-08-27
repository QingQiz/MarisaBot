namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataBotUnmute: MessageData
{
    public readonly long GroupId;

    public MessageDataBotUnmute(long groupId)
    {
        GroupId = groupId;
    }

    public override MessageDataType Type => MessageDataType.BotUnmute;
}