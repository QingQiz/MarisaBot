#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Plugin.Shared.Configuration;

public class TodayFortuneEvent
{
    private string[]? _positive;
    private string[]? _negative;

    [YamlMember(Alias = "event", ApplyNamingConventions = false)]
    public string EventName { get; set; }

    public string[] Positive
    {
        get => ConfigurationManager.RequireArray($"fortune.events[{EventName}].positive", _positive);
        set => _positive = value;
    }

    public string[] Negative
    {
        get => ConfigurationManager.RequireArray($"fortune.events[{EventName}].negative", _negative);
        set => _negative = value;
    }
}
