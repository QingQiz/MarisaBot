using QQBot.MiraiHttp.Entity.MessageData;

namespace QQBot.MiraiHttp.Entity
{
    public class Message
    {
        // Info
        public GroupInfo? GroupInfo;
        public MessageSenderInfo? Sender;
        public readonly MessageChain? MessageChain;
        
        // Control
        public string Command;
        public MiraiMessageType Type;

        public Message(IEnumerable<MessageData.MessageData> message)
        {
            MessageChain = new MessageChain(message);
            Command      = MessageChain.PlainText;
        }

        public Message(MessageChain chain)
        {
            MessageChain = chain;
            Command      = MessageChain.PlainText;
        }

        public SourceMessage Source =>
            (MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageType.Source) as SourceMessage)!;

        /// <summary>
        /// 判断消息是否 AT 某人
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool At(long target)
        {
            return MessageChain!.Messages.Any(m => m.Type == MessageType.At && (m as AtMessage)!.Target == target);
        }

        /// <summary>
        /// 消息在哪，群或者私聊
        /// </summary>
        public long Location => Type switch
        {
            MiraiMessageType.GroupMessage    => GroupInfo!.Id,
            MiraiMessageType.FriendMessage   => Sender!.Id,
            MiraiMessageType.TempMessage     => Sender!.Id,
            MiraiMessageType.StrangerMessage => Sender!.Id,
            _                                => throw new ArgumentOutOfRangeException()
        };
    }
}