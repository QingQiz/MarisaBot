#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class User
{
    [JsonProperty("avatar_url", Required = Required.Always)]
    public Uri AvatarUrl { get; set; }

    [JsonProperty("country_code", Required = Required.Always)]
    public string RegionCode { get; set; }

    [JsonProperty("default_group", Required = Required.Always)]
    public string DefaultGroup { get; set; }

    [JsonProperty("id", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("is_active", Required = Required.Always)]
    public bool IsActive { get; set; }

    [JsonProperty("is_bot", Required = Required.Always)]
    public bool IsBot { get; set; }

    [JsonProperty("is_deleted", Required = Required.Always)]
    public bool IsDeleted { get; set; }

    [JsonProperty("is_online", Required = Required.Always)]
    public bool IsOnline { get; set; }

    [JsonProperty("is_supporter", Required = Required.Always)]
    public bool IsSupporter { get; set; }

    [JsonProperty("last_visit")]
    public DateTimeOffset? LastVisit { get; set; }

    [JsonProperty("pm_friends_only", Required = Required.Always)]
    public bool PmFriendsOnly { get; set; }

    [JsonProperty("profile_colour")]
    public object? ProfileColour { get; set; }

    [JsonProperty("username", Required = Required.Always)]
    public string Username { get; set; }
}