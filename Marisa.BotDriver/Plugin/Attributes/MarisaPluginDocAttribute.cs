namespace Marisa.BotDriver.Plugin.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class MarisaPluginDocAttribute: Attribute
{
    public readonly string Doc;
    public readonly string? ParamDesc;

    public MarisaPluginDocAttribute(string doc, string? paramDesc = null)
    {
        Doc = doc;
        ParamDesc = paramDesc;
    }
}