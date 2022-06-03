namespace Marisa.BotDriver.Plugin.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class MarisaPluginDocAttribute: Attribute
{
    public readonly string Doc;

    public MarisaPluginDocAttribute(string doc)
    {
        Doc = doc;
    }
}