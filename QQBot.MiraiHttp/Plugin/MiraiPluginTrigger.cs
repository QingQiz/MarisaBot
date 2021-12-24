using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp.DI;
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
        Target = target;

        PluginTrigger t;

        if (triggerType.GetField(triggerName) != null)
        {
            var field = triggerType.GetField(triggerName)!.GetValue(null)!;
            var del   = field.GetType().GetMethod("Invoke")!;

            t = (a, b) => (bool)del.Invoke(field, new object[] { a, b })!;
        }
        else if (triggerType.GetProperty(triggerName) != null)
        {
            var field = triggerType.GetProperty(triggerName)!.GetValue(null)!;
            var del   = field.GetType().GetMethod("Invoke")!;

            t = (a, b) => (bool)del.Invoke(field, new object[] { a, b })!;
        }
        else if (triggerType.GetMethod(triggerName) != null)
        {
            var del = triggerType.GetMethod(triggerName)!;

            t = (a, b) => (bool)del.Invoke(triggerType, new object[] { a, b })!;
        }
        else
        {
            throw new ArgumentException($"Invalid trigger: {triggerType}.{triggerName}");
        }

        Trigger = (message, provider) => (message.Type & Target) != 0 && t(message, provider);
    }

    // 这里提供一些常用的 trigger
    #region Triggers

    /// <summary>
    /// At bot
    /// </summary>
    public static PluginTrigger AtBotTrigger =>
        (message, provider) => message.At(provider.GetService<DictionaryProvider>()!["QQ"]);

    #endregion
}