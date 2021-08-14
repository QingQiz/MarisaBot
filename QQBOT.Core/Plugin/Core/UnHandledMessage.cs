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
        protected override Task FriendMessageHandler(MiraiHttpSession session, IFriendInfo sender, string message)
        {
            return Task.CompletedTask;
        }

        protected override Task GroupMessageHandler(MiraiHttpSession session, IGroupMemberInfo sender, string message)
        {
            return Task.CompletedTask;
        }
    }
}