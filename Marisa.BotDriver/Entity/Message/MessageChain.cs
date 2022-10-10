using Marisa.BotDriver.Entity.MessageData;

namespace Marisa.BotDriver.Entity.Message;

public class MessageChain
{
    public readonly List<MessageData.MessageData> Messages;

    public MessageChain(params MessageData.MessageData[] messages)
    {
        Messages = messages.ToList();
    }

    public bool EnableReference = true;

    public bool CanBeReferenced => EnableReference && Messages.All(m => m.Type is not (
        MessageDataType.Voice or
        MessageDataType.Nudge or
        MessageDataType.NewMember or
        MessageDataType.MemberLeave
        )
    );

    public static MessageChain FromText(string text)
    {
        return new MessageChain(new MessageDataText(text));
    }

    public static MessageChain FromImageB64(string b64)
    {
        return new MessageChain(MessageDataImage.FromBase64(b64));
    }

    public static MessageChain FromVoiceB64(string b64)
    {
        return new MessageChain(MessageDataVoice.FromBase64(b64));
    }

    public MessageChain(IEnumerable<dynamic> data)
    {
        Messages ??= new List<MessageData.MessageData>();

        foreach (var m in data)
        {
            switch (m.type)
            {
                case "Source":
                    Messages.Add(new MessageDataId(m.id, m.time));
                    break;
                case "Plain":
                    Messages.Add(new MessageDataText(m.text));
                    break;
                case "At":
                    Messages.Add(new MessageDataAt(m.target, m.display));
                    break;
                case "Image":
                    Messages.Add(MessageDataImage.FromUrl(m.url));
                    break;
                default:
                    Messages.Add(new MessageDataUnknown());
                    break;
            }
        }
    }

    public override string ToString()
    {
        return string.Join(' ', Messages.Select(m =>
        {
            return m.Type switch
            {
                MessageDataType.Text => (m as MessageDataText)!.Text,
                _                    => $"[:{m.Type.ToString()}:]"
            };
        }));
    }

    public string Text => string.Join(' ',
        Messages.Where(m => m.Type == MessageDataType.Text).Select(m => (m as MessageDataText)!.Text));
}