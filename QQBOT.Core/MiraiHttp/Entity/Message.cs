#nullable enable
namespace QQBOT.Core.MiraiHttp.Entity
{
    public class Message
    {
        public GroupInfo? GroupInfo;
        public MessageSenderInfo? Sender;

        public MessageChain? MessageChain;
    }
}