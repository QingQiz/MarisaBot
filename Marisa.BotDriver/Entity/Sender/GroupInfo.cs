namespace Marisa.BotDriver.Entity.Sender;

public class GroupInfo
{
    public long Id;
    public string Name;
    public string Permission;

    public GroupInfo(long id, string name, string permission)
    {
        Id         = id;
        Name       = name;
        Permission = permission;
    }
}