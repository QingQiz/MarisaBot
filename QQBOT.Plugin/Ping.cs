using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin
{
    [MiraiPlugin]
    public class Ping : MiraiPluginBase
    {
        protected override async Task<MiraiPluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == ":ping")
            {
                await session.SendFriendMessage(new Message(MessageChain.FromPlainText("ping")), message.Sender!.Id);
                return MiraiPluginTaskState.CompletedTask;
            }

            return MiraiPluginTaskState.NoResponse;
        }

        protected override async Task<MiraiPluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == "ping" && message.At(session.Id))
            {
                var source = message.Source.Id;
                await session.SendGroupMessage(new Message(MessageChain.FromPlainText("ping")), message.GroupInfo!.Id,
                    source);
                return MiraiPluginTaskState.CompletedTask;
            }

            return MiraiPluginTaskState.NoResponse;
        }
    }
}