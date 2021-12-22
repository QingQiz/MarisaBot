namespace QQBot.MiraiHttp.Plugin;


[AttributeUsage(AttributeTargets.Class)]
public class MiraiPlugin : Attribute
{
    public readonly long Priority;

    /// <param name="priority">插件的优先级。高则先处理消息</param>
    public MiraiPlugin(long priority = 0)
    {
        Priority = priority;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MiraiPluginDisabled : Attribute
{
}