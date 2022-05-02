using Marisa.BotDriver.Entity.Message;
using Marisa.Utils;

namespace Marisa.BotDriver.Plugin.Trigger;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class MarisaPluginCommand: Attribute
{
    private readonly string[] _prefixes;
    private readonly StringComparison _comparison;
    private readonly MessageType _target;
    private readonly bool _strict;

    public MarisaPluginCommand(params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = StringComparison.Ordinal;
        _prefixes   = prefixes;
        _strict     = false;
    }

    public MarisaPluginCommand(bool strict = false, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = StringComparison.Ordinal;
        _prefixes   = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(MessageType target, bool strict = false, params string[] prefixes)
    {
        _target     = target;
        _comparison = StringComparison.Ordinal;
        _prefixes   = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(MessageType target, StringComparison comparison, bool strict = false, params string[] prefixes)
    {
        _target     = target;
        _comparison = comparison;
        _prefixes   = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(MessageType target, StringComparison comparison, params string[] prefixes)
    {
        _target     = target;
        _comparison = comparison;
        _prefixes   = prefixes;
        _strict     = false;
    }

    public MarisaPluginCommand(StringComparison comparison, bool strict = false, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = comparison;
        _prefixes   = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(StringComparison comparison, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = comparison;
        _prefixes   = prefixes;
        _strict     = false;
    }

    public bool Check(Message message)
    {
        if ((message.Type & _target) == 0) return false;
        if (_prefixes.Length         == 0) return true;
        
        return _strict
            ? _prefixes.Any(p => p.Equals(message.Command.Trim(), _comparison))
            : message.Command.Trim().StartWith(_prefixes, _comparison);
    }

    public string Trim(Message message)
    {
        return _prefixes.Length == 0 ? message.Command : message.Command.Trim().TrimStart(_prefixes)!.Trim();
    }
}