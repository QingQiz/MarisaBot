namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataText : MessageData
{
    public MessageDataText(string text)
    {
        Type = MessageDataType.Text;
        Text = text;
    }

    public readonly string Text;
}