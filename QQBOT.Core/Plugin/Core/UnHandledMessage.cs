using System.Threading.Tasks;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using QQBOT.Core.Attribute;

namespace QQBOT.Core.Plugin.Core
{
    [MiraiPlugin]
    [MiraiPluginDisabled]
    public class UnHandledMessage : PluginBase
    {
        protected override Task FriendMessageHandler(MiraiHttpSession session, IFriendMessageEventArgs e)
        {
            return Task.CompletedTask;
        }

        protected override Task GroupMessageHandler(MiraiHttpSession session, IGroupMessageEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
}