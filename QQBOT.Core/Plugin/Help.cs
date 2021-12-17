using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class Help : PluginBase
    {
        private MessageChain Handler(string msg)
        {
            string[] commandPrefix = { "help", "帮助", "h" };

            msg = msg.TrimStart(commandPrefix);

            if (msg == null) return null;

            if (string.IsNullOrEmpty(msg.Trim()))
                return MessageChain.FromPlainText("帮助见 https://github.com/QingQiz/QQBOT#%E5%8A%9F%E8%83%BD");

            return null;
        }

        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = Handler(message.MessageChain!.PlainText);

            if (mc == null) return PluginTaskState.NoResponse;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);

            return PluginTaskState.CompletedTask;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = Handler(message.MessageChain!.PlainText);

            if (mc == null) return PluginTaskState.NoResponse;

            if (!message.At(session.Id))
                return PluginTaskState.NoResponse;

            var source = message.Source.Id;
            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);

            return PluginTaskState.CompletedTask;
        }
    }
}