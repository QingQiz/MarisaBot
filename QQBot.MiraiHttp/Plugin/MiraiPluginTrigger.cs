using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp.Plugin;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class MiraiPluginTrigger: Attribute
{
    public delegate bool PluginTrigger(Message message, IServiceProvider provider);

    public readonly PluginTrigger Trigger;
    public readonly MiraiMessageType Target;


    /// <summary>
    /// 使用触发器的形式触发插件，当传入的触发器返回 true 时，插件被触发
    /// </summary>
    /// <param name="triggerType">类名</param>
    /// <param name="triggerName">委托名</param>
    /// <param name="target">触发器面对的消息类型</param>
    public MiraiPluginTrigger(Type triggerType, string triggerName, MiraiMessageType target = (MiraiMessageType)0b11)
    {
        Target  = target;
        Trigger = (PluginTrigger)Delegate.CreateDelegate(typeof(PluginTrigger), triggerType, triggerName);
    }
}