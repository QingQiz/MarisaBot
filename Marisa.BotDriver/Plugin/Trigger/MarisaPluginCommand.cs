using Marisa.BotDriver.Entity.Message;
using Marisa.Utils;

namespace Marisa.BotDriver.Plugin.Trigger;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class MarisaPluginCommand : Attribute
{
    private readonly StringComparison _comparison;
    private readonly MessageType _target;
    private readonly bool _strict;

    private bool Comparer(string a, string b)
    {
        return _strict ? string.Equals(a, b, _comparison) : a.StartsWith(b, _comparison);
    }

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

    public bool TryMatch(Message message, out string afterMatch)
    {
        afterMatch = message.Command;

        if ((message.Type & _target) == 0) return false;
        if (Commands.Length          == 0) return true;

        afterMatch = afterMatch.Trim();

        foreach (var prefix in Commands)
        {
            if (!Comparer(afterMatch, prefix)) continue;

            afterMatch = afterMatch[prefix.Length..].TrimStart();
            return true;
        }

        return false;
    }
}