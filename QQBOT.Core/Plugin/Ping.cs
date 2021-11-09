using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class Ping : PluginBase
    {
        public override async Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == ":ping")
            {
                await session.SendFriendMessage(new Message(MessageChain.FromPlainText("ping")), message.Sender!.Id);
            }
        }

        public override async Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == "ping")
            {
                if (message.MessageChain!.Messages.Any(m =>
                    m.Type == MessageType.At && (m as AtMessage)!.Target == session.Id))
                {
                    var source = (message.MessageChain!.Messages.First(m => m.Type == MessageType.Source) as SourceMessage)!.Id;
                    await session.SendGroupMessage(new Message(MessageChain.FromPlainText("ping")), message.GroupInfo!.Id, source);
                }
            }
        }
    }
}