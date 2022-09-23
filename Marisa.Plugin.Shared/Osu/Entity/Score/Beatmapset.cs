#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Beatmapset
{
    [JsonProperty("artist", Required = Required.Always)]
    public string Artist { get; set; }

    [JsonProperty("artist_unicode", Required = Required.Always)]
    public string ArtistUnicode { get; set; }

    [JsonProperty("covers", Required = Required.Always)]
    public Covers Covers { get; set; }

    [JsonProperty("creator", Required = Required.Always)]
    public string Creator { get; set; }

    [JsonProperty("favourite_count", Required = Required.Always)]
    public long FavouriteCount { get; set; }

    [JsonProperty("hype")]
    public object? Hype { get; set; }

    [JsonProperty("id", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("nsfw", Required = Required.Always)]
    public bool Nsfw { get; set; }

    [JsonProperty("offset", Required = Required.Always)]
    public long Offset { get; set; }

    [JsonProperty("play_count", Required = Required.Always)]
    public long PlayCount { get; set; }

    [JsonProperty("preview_url", Required = Required.Always)]
    public string PreviewUrl { get; set; }

    [JsonProperty("source", Required = Required.Always)]
    public string Source { get; set; }

    [JsonProperty("spotlight", Required = Required.Always)]
    public bool Spotlight { get; set; }

    [JsonProperty("status", Required = Required.Always)]
    public string Status { get; set; }

    [JsonProperty("title", Required = Required.Always)]
    public string Title { get; set; }

    [JsonProperty("title_unicode", Required = Required.Always)]
    public string TitleUnicode { get; set; }

    [JsonProperty("track_id")]
    public object? TrackId { get; set; }

    [JsonProperty("user_id", Required = Required.Always)]
    public long UserId { get; set; }

    [JsonProperty("video", Required = Required.Always)]
    public bool Video { get; set; }
}