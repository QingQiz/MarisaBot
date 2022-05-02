namespace Marisa.BotDriver.Plugin.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class MarisaPluginAttribute : Attribute
{
    public readonly long Priority;

    /// <param name="priority">插件的优先级。高则先处理消息</param>
    public MarisaPluginAttribute(long priority = 0)
    {
        Priority = priority;
    }
}