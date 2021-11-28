using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class Ping : PluginBase
    {
        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == ":ping")
            {
                await session.SendFriendMessage(new Message(MessageChain.FromPlainText("ping")), message.Sender!.Id);
                return PluginTaskState.CompletedTask;
            }

            return PluginTaskState.ToBeContinued;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == "ping")
            {
                if (message.MessageChain!.Messages.Any(m =>
                    m.Type == MessageType.At && (m as AtMessage)!.Target == session.Id))
                {
                    var source = message.Source.Id;
                    await session.SendGroupMessage(new Message(MessageChain.FromPlainText("ping")), message.GroupInfo!.Id, source);
                    return PluginTaskState.CompletedTask;
                }
            }

            return PluginTaskState.ToBeContinued;
        }
    }
}