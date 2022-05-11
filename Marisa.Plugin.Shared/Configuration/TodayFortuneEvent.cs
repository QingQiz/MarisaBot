#pragma warning disable CS8618

using YamlDotNet.Serialization;

namespace Marisa.Plugin.Shared.Configuration;

public class TodayFortuneEvent
{
    [YamlMember(Alias = "event", ApplyNamingConventions = false)]
    public string EventName { get; set; }

    public string[] Positive { get; set; }
    public string[] Negative { get; set; }
}