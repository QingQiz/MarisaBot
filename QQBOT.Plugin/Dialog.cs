using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin
{
    [MiraiPlugin(1000)]
    public class Dialog : MiraiPluginBase
    {
        public new delegate Task<MiraiPluginTaskState> MessageHandler(MiraiHttpSession session, Message message);

        private static readonly Dictionary<long, MessageHandler> Handlers = new();

        public static bool AddHandler(long groupId, MessageHandler handler)
        {
            lock (Handlers)
            {
                if (Handlers.ContainsKey(groupId)) return false;

                Handlers[groupId] = handler;
                return true;
            }
        }

#pragma warning disable 1998
        protected override async Task<MiraiPluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            lock (Handlers)
            {
                if (Handlers.Count == 0) return MiraiPluginTaskState.NoResponse;

                var groupId = message.GroupInfo!.Id;

                if (!Handlers.ContainsKey(groupId)) return MiraiPluginTaskState.NoResponse;

                var handler = Handlers[groupId];
                switch (handler(session, message).Result)
                {
                    // 完成了，删除 handler
                    case MiraiPluginTaskState.CompletedTask:
                        Handlers.Remove(groupId);
                        return MiraiPluginTaskState.CompletedTask;
                    // 处理了一部分，但没有完成，不删除，但是终止 event 传播
                    case MiraiPluginTaskState.ToBeContinued:
                        return MiraiPluginTaskState.CompletedTask;
                    // handler 没处理，交给其它插件处理
                    case MiraiPluginTaskState.NoResponse:
                        return MiraiPluginTaskState.NoResponse;
                    // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
                    default:
                        Handlers.Remove(groupId);
                        return MiraiPluginTaskState.NoResponse;
                }
            }
        }
#pragma warning restore 1998
    }
}