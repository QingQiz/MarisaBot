#nullable enable
namespace QQBot.MiraiHttp.Entity
{
    public class MessageSenderInfo
    {
        public long Id;
        public string Name;
        public string? Remark;
        public string? Permission;

        public MessageSenderInfo(long id, string name, string? remark = null, string? permission = null)
        {
            Id         = id;
            Name       = name;
            Remark     = remark;
            Permission = permission;
        }
    }
}