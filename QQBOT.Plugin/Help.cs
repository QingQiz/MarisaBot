using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.MiraiHttp.Util;

namespace QQBot.Plugin
{
    [MiraiPlugin]
    public class Help : MiraiPluginBase
    {
        private MessageChain? Handler(string msg)
        {
            string[] commandPrefix = { "help", "帮助", "h" };

            var m = msg.TrimStart(commandPrefix);

            if (m == null) return null;

            if (string.IsNullOrEmpty(msg.Trim()))
                return MessageChain.FromPlainText("帮助见 https://github.com/QingQiz/QQBOT#%E5%8A%9F%E8%83%BD");

            return null;
        }

        protected override async Task<MiraiPluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = Handler(message.MessageChain!.PlainText);

            if (mc == null) return MiraiPluginTaskState.NoResponse;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);

            return MiraiPluginTaskState.CompletedTask;
        }

        protected override async Task<MiraiPluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = Handler(message.MessageChain!.PlainText);

            if (mc == null) return MiraiPluginTaskState.NoResponse;

            if (!message.At(session.Id))
                return MiraiPluginTaskState.NoResponse;

            var source = message.Source.Id;
            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);

            return MiraiPluginTaskState.CompletedTask;
        }
    }
}