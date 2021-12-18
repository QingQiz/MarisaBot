using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp.Plugin
{
    /// <summary>
    /// 插件的默认消息调用顺序:
    ///  message -> <see cref="MessageHandlerWrapper">MessageHandlerWrapper</see> -> (Friend|Group|..)MessageHandler ->
    /// MessageHandlerWrapper -> MessageHandler
    /// <br/>
    /// 这样设计的原因是：1）方便同时处理所有渠道的消息，并进行异常捕获；2）特殊的功能也可针对不同渠道的消息进行特殊的实现，例如覆写 FriendMessageHandler
    /// </summary>
    [MiraiPlugin]
    [MiraiPluginDisabled]
    public abstract class MiraiPluginBase
    {
        protected virtual async Task<MiraiPluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = await MessageHandlerWrapper(session, message, MiraiMessageType.FriendMessage);

            if (mc == null) return MiraiPluginTaskState.NoResponse;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendFriendMessage(new Message(m), message.Sender!.Id);
            }

            return proceed ? MiraiPluginTaskState.CompletedTask : MiraiPluginTaskState.NoResponse;
        }

        protected virtual async Task<MiraiPluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc = await MessageHandlerWrapper(session, message, MiraiMessageType.GroupMessage);

            if (mc == null) return MiraiPluginTaskState.NoResponse;

            var source = message.Source.Id;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendGroupMessage(new Message(m), message.GroupInfo!.Id, m.CanReference ? source : null);
            }

            return proceed ? MiraiPluginTaskState.CompletedTask : MiraiPluginTaskState.NoResponse;
        }

        protected virtual async Task<MiraiPluginTaskState> TempMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(MiraiPluginTaskState.NoResponse);
        }

        protected virtual async Task<MiraiPluginTaskState> StrangerMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(MiraiPluginTaskState.NoResponse);
        }

        protected virtual async Task<MiraiPluginTaskState> EventHandler(MiraiHttpSession session, dynamic data)
        {
            return await Task.FromResult(MiraiPluginTaskState.NoResponse);
        }

        /// <summary>
        /// 这个函数是给外界调用的。当来了一个消息，由这个函数决定送往哪个处理函数
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Task MessageHandlerWrapper(MiraiHttpSession session, Message message, MiraiMessageType type,
            ref MiraiPluginTaskState state)
        {
            if (state == MiraiPluginTaskState.CompletedTask) return Task.CompletedTask;

            state = type switch
            {
                MiraiMessageType.FriendMessage   => FriendMessageHandler(session, message).Result,
                MiraiMessageType.GroupMessage    => GroupMessageHandler(session, message).Result,
                MiraiMessageType.StrangerMessage => StrangerMessageHandler(session, message).Result,
                MiraiMessageType.TempMessage     => TempMessageHandler(session, message).Result,
                _                                => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            return Task.CompletedTask;
        }

        public Task EventHandlerWrapper(MiraiHttpSession session, dynamic data, ref MiraiPluginTaskState state)
        {
            if (state == MiraiPluginTaskState.CompletedTask) return Task.CompletedTask;

            state = EventHandler(session, data).Result;

            return Task.CompletedTask;
        }

        /// <summary>
        /// 对 <see cref="MessageHandler"/> 的包装，包含了异常处理部分。当某个处理函数被调用时，会默认调用这个函数来处理
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        protected virtual async Task<IAsyncEnumerable<MessageChain?>?> MessageHandlerWrapper(MiraiHttpSession session,
            Message message, MiraiMessageType type)
        {
            try
            {
                return MessageHandler(message, type);
            }
            catch (Exception e)
            {
                if (message.GroupInfo == null)
                    await session.SendFriendMessage(new Message(MessageChain.FromPlainText(e.ToString())),
                        message.Sender!.Id);
                else
                    await session.SendGroupMessage(new Message(MessageChain.FromPlainText(e.ToString())),
                        message.GroupInfo!.Id);
                throw;
            }
        }

        protected virtual IAsyncEnumerable<MessageChain?>? MessageHandler(Message message, MiraiMessageType type)
        {
            return null;
        }
    }
}