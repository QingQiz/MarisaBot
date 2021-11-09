using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class Help : PluginBase
    {
        private MessageChain Handler(string msg)
        {
            string[] commandPrefix = { "help", "帮助", "h" };

            msg = msg.TrimStart(commandPrefix).Trim();

            if (string.IsNullOrEmpty(msg))
            {
                return MessageChain.FromPlainText("帮助见 https://github.com/QingQiz/QQBOT#%E5%8A%9F%E8%83%BD");
            }

            return null;
        }

        public override async Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = Handler(message.MessageChain!.PlainText);

            if (mc == null) return;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);
        }

        public override async Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = Handler(message.MessageChain!.PlainText);

            if (mc == null) return;

            if (message.MessageChain!.Messages.Any(m =>
                m.Type == MessageType.At && (m as AtMessage)!.Target == session.Id))
            {
                var source =
                    (message.MessageChain!.Messages.First(m => m.Type == MessageType.Source) as SourceMessage)!.Id;

                await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);
            }
        }
    }
}