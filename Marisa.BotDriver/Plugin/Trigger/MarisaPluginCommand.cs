using Marisa.BotDriver.Entity.Message;

namespace Marisa.BotDriver.Plugin.Trigger;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class MarisaPluginCommand(MessageType target, StringComparison comparison, bool strict = false, params string[] prefixes)
    : Attribute
{
    private bool Comparer(ReadOnlyMemory<char> a, ReadOnlyMemory<char> b)
    {
        return strict ? a.Span.Equals(b.Span, comparison) : a.Span.StartsWith(b.Span, comparison);
    }

    public ReadOnlyMemory<char>[] Commands { get; } = prefixes.Select(p => p.AsMemory()).ToArray();

    public MarisaPluginCommand(params string[] prefixes) : this((MessageType)0b11, StringComparison.OrdinalIgnoreCase, false, prefixes)
    {
    }

    public MarisaPluginCommand(bool strict = false, params string[] prefixes) : this((MessageType)0b11, StringComparison.OrdinalIgnoreCase, strict, prefixes)
    {
    }

    public MarisaPluginCommand(MessageType target, bool strict = false, params string[] prefixes) : this(target, StringComparison.OrdinalIgnoreCase, strict, prefixes)
    {
    }

    public MarisaPluginCommand(MessageType target, StringComparison comparison, params string[] prefixes) : this(target, comparison, false, prefixes)
    {
    }

    public MarisaPluginCommand(StringComparison comparison, bool strict = false, params string[] prefixes) : this((MessageType)0b11, comparison, strict, prefixes)
    {
    }

    public MarisaPluginCommand(StringComparison comparison, params string[] prefixes) : this((MessageType)0b11, comparison, false, prefixes)
    {
    }

    public bool TryMatch(Message message, out ReadOnlyMemory<char> afterMatch)
    {
        afterMatch = message.Command;

        if ((message.Type & target) == 0 && message.Type != 0) return false;
        if (Commands.Length == 0) return true;

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