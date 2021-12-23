using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Util;


namespace QQBot.MiraiHttp.Plugin;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class MiraiPluginCommand: Attribute
{
    private readonly string[] _prefixes;
    private readonly StringComparison _comparison;
    public readonly MiraiMessageType Target;

    public MiraiPluginCommand(params string[] prefixes)
    {
        Target      = (MiraiMessageType)0b11;
        _comparison = StringComparison.Ordinal;
        _prefixes   = prefixes;
    }

    public MiraiPluginCommand(MiraiMessageType target, StringComparison comparison, params string[] prefixes)
    {
        Target      = target;
        _comparison = comparison;
        _prefixes   = prefixes;
    }

    public bool Check(Message message)
    {
        return _prefixes.Length == 0 || message.Command.StartWith(_prefixes, _comparison);
    }

    public string Trim(Message message)
    {
        return _prefixes.Length == 0 ? message.Command : message.Command.TrimStart(_prefixes)!;
    }
}