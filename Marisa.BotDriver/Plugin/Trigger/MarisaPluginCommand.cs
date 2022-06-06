using Marisa.BotDriver.Entity.Message;
using Marisa.Utils;

namespace Marisa.BotDriver.Plugin.Trigger;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class MarisaPluginCommand : Attribute
{
    private readonly StringComparison _comparison;
    private readonly MessageType _target;
    private readonly bool _strict;

    public string[] Commands { get; }

    public MarisaPluginCommand(params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = StringComparison.OrdinalIgnoreCase;
        Commands    = prefixes;
        _strict     = false;
    }

    public MarisaPluginCommand(bool strict = false, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = StringComparison.OrdinalIgnoreCase;
        Commands    = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(MessageType target, bool strict = false, params string[] prefixes)
    {
        _target     = target;
        _comparison = StringComparison.OrdinalIgnoreCase;
        Commands    = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(
        MessageType target, StringComparison comparison, bool strict = false, params string[] prefixes)
    {
        _target     = target;
        _comparison = comparison;
        Commands    = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(MessageType target, StringComparison comparison, params string[] prefixes)
    {
        _target     = target;
        _comparison = comparison;
        Commands    = prefixes;
        _strict     = false;
    }

    public MarisaPluginCommand(StringComparison comparison, bool strict = false, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = comparison;
        Commands    = prefixes;
        _strict     = strict;
    }

    public MarisaPluginCommand(StringComparison comparison, params string[] prefixes)
    {
        _target     = (MessageType)0b11;
        _comparison = comparison;
        Commands    = prefixes;
        _strict     = false;
    }

    public bool Check(Message message)
    {
        if ((message.Type & _target) == 0) return false;
        if (Commands.Length == 0) return true;

        return _strict
            ? Commands.Any(p => p.Equals(message.Command.Trim(), _comparison))
            : message.Command.Trim().StartWith(Commands, _comparison);
    }

    public string Trim(Message message)
    {
        return Commands.Length == 0 ? message.Command : message.Command.Trim().TrimStart(Commands)!.Trim();
    }
}