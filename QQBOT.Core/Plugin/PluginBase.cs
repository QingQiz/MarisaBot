using System.Threading.Tasks;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using Mirai_CSharp.Plugin.Interfaces;
using QQBOT.Core.Attribute;
using QQBOT.Core.Utils;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    [MiraiPluginDisabled]
    public abstract class PluginBase : IFriendMessage, IGroupMessage, ITempMessage, IUnknownMessage
    {
        #region Handler

        protected abstract Task<bool> FriendMessageHandler(MiraiHttpSession session, IFriendMessageEventArgs e);

        protected abstract Task<bool> GroupMessageHandler(MiraiHttpSession session, IGroupMessageEventArgs e);

        protected virtual async Task<bool> TempMessageHandler(MiraiHttpSession session, ITempMessageEventArgs e)
        {
#pragma warning disable 618
            return await GroupMessageHandler(session, new GroupMessageEventArgs
            {
                Chain = e.Chain,
                Sender = e.Sender
            });
#pragma warning restore 618
        }

        protected virtual async Task<bool> UnknownMessageHandler(MiraiHttpSession session,
            IUnknownMessageEventArgs args)
        {
            return await Task.Run(() => true);
        }

        #endregion

        #region Handler Wrapper

        public async Task<bool> FriendMessage(MiraiHttpSession session, IFriendMessageEventArgs e)
        {
            await Logger.FriendMessage(e);
            return await FriendMessageHandler(session, e);
        }

        public async Task<bool> GroupMessage(MiraiHttpSession session, IGroupMessageEventArgs e)
        {
            await Logger.GroupMessage(e);
            return await GroupMessageHandler(session, e);
        }

        public async Task<bool> TempMessage(MiraiHttpSession session, ITempMessageEventArgs e)
        {
            await Logger.TempMessage(e);
            return await TempMessageHandler(session, e);
        }

        public async Task<bool> UnknownMessage(MiraiHttpSession session, IUnknownMessageEventArgs e)
        {
            await Logger.UnknownMessage(e);
            return await UnknownMessageHandler(session, e);
        }

        #endregion
    }
}