namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataBotMute : MessageData
{
    public override MessageDataType Type => MessageDataType.BotMute;

    public readonly long GroupId;
    public readonly TimeSpan Time;

    public MessageDataBotMute(long groupId, long? seconds = null)
    {
        GroupId = groupId;
        Time    = seconds == null ? TimeSpan.MaxValue : TimeSpan.FromSeconds((double)seconds);
    }
}