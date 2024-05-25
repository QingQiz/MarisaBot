namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataSignServerLose(string text) : MessageData
{
    public override MessageDataType Type => MessageDataType.Unknown;

    public string Text { get; set; } = text;
}