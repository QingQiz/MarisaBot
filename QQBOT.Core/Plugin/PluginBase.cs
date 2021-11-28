using System;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    [MiraiPluginDisabled]
    public abstract class PluginBase
    {
        protected virtual async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(PluginTaskState.ToBeContinued);
        }

        protected virtual async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(PluginTaskState.ToBeContinued);
        }

        protected virtual async Task<PluginTaskState> TempMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(PluginTaskState.ToBeContinued);
        }

        protected virtual async Task<PluginTaskState> StrangerMessageHandler(MiraiHttpSession session, Message message)
        {
            return await Task.FromResult(PluginTaskState.ToBeContinued);
        }

        protected virtual async Task<PluginTaskState> EventHandler(MiraiHttpSession session, dynamic data)
        {
            return await Task.FromResult(PluginTaskState.ToBeContinued);
        }

        public Task MessageHandlerWrapper(MiraiHttpSession session, Message message, MiraiMessageType type,
            ref PluginTaskState state)
        {
            if (state == PluginTaskState.CompletedTask)
            {
                return Task.CompletedTask;
            }

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

        public Task EventHandlerWrapper(MiraiHttpSession session, dynamic data, ref PluginTaskState state)
        {
            if (state == PluginTaskState.CompletedTask)
            {
                return Task.CompletedTask;
            }

            state = EventHandler(session, data).Result;
            
            return Task.CompletedTask;
        }
    }
}