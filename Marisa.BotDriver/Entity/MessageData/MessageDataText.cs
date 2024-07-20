namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataText : MessageData
{
    public ReadOnlyMemory<char> Text;

    public MessageDataText(string text)
    {
        Text = text.AsMemory();
    }

    public MessageDataText(ReadOnlyMemory<char> text)
    {
        Text = text;
    }

    public override MessageDataType Type => MessageDataType.Text;
}