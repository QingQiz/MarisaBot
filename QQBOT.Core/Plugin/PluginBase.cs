using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    [MiraiPluginDisabled]
    public abstract class PluginBase
    {
        public virtual Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            return Task.CompletedTask;
        }

        public virtual Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            return Task.CompletedTask;
        }

        public virtual Task TempMessageHandler(MiraiHttpSession session, Message message)
        {
            return Task.CompletedTask;
        }

        public virtual Task StrangerMessageHandler(MiraiHttpSession session, Message message)
        {
            return Task.CompletedTask;
        }
    }
}