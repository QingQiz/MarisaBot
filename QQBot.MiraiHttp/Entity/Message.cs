using QQBot.MiraiHttp.Entity.MessageData;

#nullable enable
namespace QQBot.MiraiHttp.Entity
{
    public class Message
    {
        public GroupInfo? GroupInfo;
        public MessageSenderInfo? Sender;

        public MessageChain? MessageChain;

        public Message()
        {
        }

        public Message(IEnumerable<MessageData.MessageData> message)
        {
            MessageChain = new MessageChain(message);
        }

        public Message(MessageChain chain)
        {
            MessageChain = chain;
        }

        public SourceMessage Source =>
            (MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageType.Source) as SourceMessage)!;

        public bool At(long target)
        {
            return MessageChain!.Messages.Any(m => m.Type == MessageType.At && (m as AtMessage)!.Target == target);
        }
    }
}