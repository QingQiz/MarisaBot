#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Beatmap
{
    [JsonProperty("beatmapset_id", Required = Required.Always)]
    public long BeatmapsetId { get; set; }

    [JsonProperty("difficulty_rating", Required = Required.Always)]
    public double DifficultyRating { get; set; }

    [JsonProperty("id", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("mode", Required = Required.Always)]
    public string Mode { get; set; }

    [JsonProperty("status", Required = Required.Always)]
    public string Status { get; set; }

    [JsonProperty("total_length", Required = Required.Always)]
    public long TotalLength { get; set; }

    [JsonProperty("user_id", Required = Required.Always)]
    public long UserId { get; set; }

    [JsonProperty("version", Required = Required.Always)]
    public string Version { get; set; }

    [JsonProperty("accuracy", Required = Required.Always)]
    public long Accuracy { get; set; }

    [JsonProperty("ar", Required = Required.Always)]
    public long Ar { get; set; }

    [JsonProperty("bpm", Required = Required.Always)]
    public long Bpm { get; set; }

    [JsonProperty("convert", Required = Required.Always)]
    public bool Convert { get; set; }

    [JsonProperty("count_circles", Required = Required.Always)]
    public long CountCircles { get; set; }

    [JsonProperty("count_sliders", Required = Required.Always)]
    public long CountSliders { get; set; }

    [JsonProperty("count_spinners", Required = Required.Always)]
    public long CountSpinners { get; set; }

    [JsonProperty("cs", Required = Required.Always)]
    public long Cs { get; set; }

    [JsonProperty("deleted_at", Required = Required.AllowNull)]
    public object DeletedAt { get; set; }

    [JsonProperty("drain", Required = Required.Always)]
    public long Drain { get; set; }

    [JsonProperty("hit_length", Required = Required.Always)]
    public long HitLength { get; set; }

    [JsonProperty("is_scoreable", Required = Required.Always)]
    public bool IsScoreable { get; set; }

    [JsonProperty("last_updated", Required = Required.Always)]
    public DateTimeOffset LastUpdated { get; set; }

    [JsonProperty("mode_int", Required = Required.Always)]
    public long ModeInt { get; set; }

    [JsonProperty("passcount", Required = Required.Always)]
    public long Passcount { get; set; }

    [JsonProperty("playcount", Required = Required.Always)]
    public long Playcount { get; set; }

    [JsonProperty("ranked", Required = Required.Always)]
    public long Ranked { get; set; }

    [JsonProperty("url", Required = Required.Always)]
    public Uri Url { get; set; }

    [JsonProperty("checksum", Required = Required.Always)]
    public string Checksum { get; set; }
}