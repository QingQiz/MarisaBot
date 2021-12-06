using System.Collections.Generic;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin(priority:1000)]
    public class Dialog : PluginBase
    {
        public delegate Task<PluginTaskState> MessageHandler(MiraiHttpSession session, Message message);

        private static readonly Dictionary<long, MessageHandler> Handlers = new ();

        public static bool AddHandler(long groupId, MessageHandler handler)
        {
            if (Handlers.ContainsKey(groupId)) return false;
            
            Handlers[groupId] = handler;
            return true;
        }

#pragma warning disable 1998
        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            if (Handlers.Count == 0) return PluginTaskState.NoResponse;

            var groupId = message.GroupInfo!.Id;

            if (!Handlers.ContainsKey(groupId)) return PluginTaskState.NoResponse;

            var handler = Handlers[groupId];

            lock (handler)
            {
                switch (handler(session, message).Result)
                {
                    // 完成了，删除 handler
                    case PluginTaskState.CompletedTask:
                        Handlers.Remove(groupId);
                        return PluginTaskState.CompletedTask;
                    // 处理了一部分，但没有完成，不删除，但是终止 event 传播
                    case PluginTaskState.ToBeContinued:
                        return PluginTaskState.CompletedTask;
                    // handler 没处理，交给其它插件处理
                    case PluginTaskState.NoResponse:
                        return PluginTaskState.NoResponse;
                    // 错误的状态，删除这个异常的 handler（虽然不太可能发生）
                    default:
                        Handlers.Remove(groupId);
                        return PluginTaskState.NoResponse;
                }
            }
        }
#pragma warning restore 1998
    }
}