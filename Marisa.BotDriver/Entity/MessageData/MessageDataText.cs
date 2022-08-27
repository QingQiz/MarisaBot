namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataText : MessageData
{
    public MessageDataText(string text)
    {
        Text = text;
    }

    public readonly string Text;

    public override MessageDataType Type => MessageDataType.Text;
}