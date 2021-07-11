using System.Threading.Tasks;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using QQBOT.Core.Attribute;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class Arcaea : PluginBase
    {
        protected override async Task<bool> FriendMessageHandler(MiraiHttpSession session, IFriendMessageEventArgs e)
        {
            return await Task.Run(() => false);
        }

        protected override async Task<bool> GroupMessageHandler(MiraiHttpSession session, IGroupMessageEventArgs e)
        {
            return await Task.Run(() => false);
        }
    }
}