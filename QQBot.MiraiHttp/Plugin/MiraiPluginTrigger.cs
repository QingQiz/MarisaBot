using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;

namespace QQBot.MiraiHttp.Plugin;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class MiraiPluginTrigger: Attribute
{
    public delegate bool PluginTrigger(Message message, IServiceProvider provider);

    public readonly PluginTrigger Trigger;


    /// <summary>
    /// 使用触发器的形式触发插件，当传入的触发器返回 true 时，插件被触发
    /// </summary>
    /// <param name="triggerType">类名</param>
    /// <param name="triggerName">委托名</param>
    /// <param name="target">触发器面对的消息类型</param>
    public MiraiPluginTrigger(Type triggerType, string triggerName, MiraiMessageType target = (MiraiMessageType)0b11)
    {
        PluginTrigger t;
        const BindingFlags bindingFlags = BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static  | BindingFlags.Public;

        if (triggerType.GetField(triggerName, bindingFlags) != null)
        {
            var field = triggerType.GetField(triggerName, bindingFlags)!.GetValue(null)!;
            var del   = field.GetType().GetMethod("Invoke", bindingFlags)!;

            t = (a, b) => (bool)del.Invoke(field, new object[] { a, b })!;
        }
        else if (triggerType.GetProperty(triggerName, bindingFlags) != null)
        {
            var field = triggerType.GetProperty(triggerName, bindingFlags)!.GetValue(null)!;
            var del   = field.GetType().GetMethod("Invoke", bindingFlags)!;

            t = (a, b) => (bool)del.Invoke(field, new object[] { a, b })!;
        }
        else if (triggerType.GetMethod(triggerName, bindingFlags) != null)
        {
            var del = triggerType.GetMethod(triggerName, bindingFlags)!;

            t = (a, b) => (bool)del.Invoke(triggerType, new object[] { a, b })!;
        }
        else
        {
            throw new ArgumentException($"Invalid trigger: {triggerType}.{triggerName}");
        }

        Trigger = (message, provider) => (message.Type & target) != 0 && t(message, provider);
    }

    // 这里提供一些常用的 trigger
    #region Triggers

    /// <summary>
    /// At bot
    /// </summary>
    public static PluginTrigger AtBotTrigger =>
        (message, provider) => message.At(provider.GetService<DictionaryProvider>()!["QQ"]);

    /// <summary>
    /// Plain Text
    /// </summary>
    public static PluginTrigger PlainTextTrigger =>
        (message, _) => message.MessageChain!.Messages.All(m => m.Type is MessageType.Plain or MessageType.Source);

    /// <summary>
    /// always return true
    /// </summary>
    public static PluginTrigger AlwaysTrueTrigger => (_, _) => true;

    #endregion
}