#pragma warning disable CS8618
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public partial class OsuUserInfo
{
    [JsonProperty("avatar_url")]
    public Uri AvatarUrl { get; set; }

    [JsonProperty("country_code")]
    public string RegionCode { get; set; }

    [JsonProperty("default_group")]
    public string DefaultGroup { get; set; }

    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }

    [JsonProperty("is_bot")]
    public bool IsBot { get; set; }

    [JsonProperty("is_deleted")]
    public bool IsDeleted { get; set; }

    [JsonProperty("is_online")]
    public bool IsOnline { get; set; }

    [JsonProperty("is_supporter")]
    public bool IsSupporter { get; set; }

    [JsonProperty("last_visit")]
    public DateTimeOffset? LastVisit { get; set; }

    [JsonProperty("pm_friends_only")]
    public bool PmFriendsOnly { get; set; }

    [JsonProperty("profile_colour")]
    public object ProfileColour { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("cover_url")]
    public Uri CoverUrl { get; set; }

    [JsonProperty("discord")]
    public object Discord { get; set; }

    [JsonProperty("has_supported")]
    public bool HasSupported { get; set; }

    [JsonProperty("interests")]
    public object Interests { get; set; }

    [JsonProperty("join_date")]
    public DateTimeOffset JoinDate { get; set; }

    [JsonProperty("kudosu")]
    public Kudosu Kudosu { get; set; }

    [JsonProperty("location")]
    public object Location { get; set; }

    [JsonProperty("max_blocks")]
    public long MaxBlocks { get; set; }

    [JsonProperty("max_friends")]
    public long MaxFriends { get; set; }

    [JsonProperty("occupation")]
    public object Occupation { get; set; }

    [JsonProperty("playmode")]
    public string Playmode { get; set; }

    [JsonProperty("playstyle")]
    public string[] PlayStyle { get; set; }

    [JsonProperty("post_count")]
    public long PostCount { get; set; }

    [JsonProperty("profile_order")]
    public string[] ProfileOrder { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("title_url")]
    public object TitleUrl { get; set; }

    [JsonProperty("twitter")]
    public object Twitter { get; set; }

    [JsonProperty("website")]
    public object Website { get; set; }

    [JsonProperty("country")]
    public Region Region { get; set; }

    [JsonProperty("cover")]
    public Cover Cover { get; set; }

    [JsonProperty("account_history")]
    public object[] AccountHistory { get; set; }

    [JsonProperty("active_tournament_banner")]
    public object ActiveTournamentBanner { get; set; }

    [JsonProperty("badges")]
    public Badge[] Badges { get; set; }

    [JsonProperty("beatmap_playcounts_count")]
    public long BeatmapPlayCountsCount { get; set; }

    [JsonProperty("comments_count")]
    public long CommentsCount { get; set; }

    [JsonProperty("favourite_beatmapset_count")]
    public long FavouriteBeatmapsetCount { get; set; }

    [JsonProperty("follower_count")]
    public long FollowerCount { get; set; }

    [JsonProperty("graveyard_beatmapset_count")]
    public long GraveyardBeatmapsetCount { get; set; }

    [JsonProperty("groups")]
    public object[] Groups { get; set; }

    [JsonProperty("guest_beatmapset_count")]
    public long GuestBeatmapsetCount { get; set; }

    [JsonProperty("loved_beatmapset_count")]
    public long LovedBeatmapsetCount { get; set; }

    [JsonProperty("mapping_follower_count")]
    public long MappingFollowerCount { get; set; }

    [JsonProperty("monthly_playcounts")]
    public MonthlyPlayCount[] MonthlyPlayCounts { get; set; }

    [JsonProperty("page")]
    public Page Page { get; set; }

    [JsonProperty("pending_beatmapset_count")]
    public long PendingBeatmapSetCount { get; set; }

    [JsonProperty("previous_usernames")]
    public string[] PreviousUsernames { get; set; }

    [JsonProperty("ranked_beatmapset_count")]
    public long RankedBeatmapSetCount { get; set; }

    [JsonProperty("replays_watched_counts")]
    public object[] ReplaysWatchedCounts { get; set; }

    [JsonProperty("scores_best_count")]
    public long ScoresBestCount { get; set; }

    [JsonProperty("scores_first_count")]
    public long ScoresFirstCount { get; set; }

    [JsonProperty("scores_pinned_count")]
    public long ScoresPinnedCount { get; set; }

    [JsonProperty("scores_recent_count")]
    public long ScoresRecentCount { get; set; }

    [JsonProperty("statistics")]
    public Statistics Statistics { get; set; }

    [JsonProperty("support_level")]
    public int SupportLevel { get; set; }

    [JsonProperty("user_achievements")]
    public UserAchievement[] UserAchievements { get; set; }

    [JsonProperty("rank_history")]
    public RankHistory? RankHistory { get; set; }

    [JsonProperty("ranked_and_approved_beatmapset_count")]
    public long RankedAndApprovedBeatmapsetCount { get; set; }

    [JsonProperty("unranked_beatmapset_count")]
    public long UnrankedBeatmapsetCount { get; set; }
}

public partial class OsuUserInfo
{
    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling        = DateParseHandling.None,
            NullValueHandling        = NullValueHandling.Ignore,
            DefaultValueHandling     = DefaultValueHandling.Populate,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            }
        };
    }

    public static OsuUserInfo FromJson(string json)
    {
        return JsonConvert.DeserializeObject<OsuUserInfo>(json, Converter.Settings)!;
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Converter.Settings);
    }
}