namespace Marisa.BotDriver.Entity.MessageSender;

public class SenderInfo
{
    public long Id;
    public string Name;
    public string? Remark;
    public string? Permission;

    public SenderInfo(long id, string name, string? remark = null, string? permission = null)
    {
        Id         = id;
        Name       = name;
        Remark     = remark;
        Permission = permission;
    }
}