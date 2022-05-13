#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class UserAchievement
{
    [JsonProperty("achieved_at")]
    public DateTimeOffset AchievedAt { get; set; }

    [JsonProperty("achievement_id")]
    public long AchievementId { get; set; }
}