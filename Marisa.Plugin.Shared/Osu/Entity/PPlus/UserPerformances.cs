using Newtonsoft.Json;

#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Osu.Entity.PPlus;

public class UserPerformances
{
    [JsonProperty("total")]
    public Accuracy[] Total { get; set; }

    [JsonProperty("aim")]
    public Accuracy[] Aim { get; set; }

    [JsonProperty("jump_aim")]
    public Accuracy[] JumpAim { get; set; }

    [JsonProperty("flow_aim")]
    public Accuracy[] FlowAim { get; set; }

    [JsonProperty("precision")]
    public Accuracy[] Precision { get; set; }

    [JsonProperty("speed")]
    public Accuracy[] Speed { get; set; }

    [JsonProperty("stamina")]
    public Accuracy[] Stamina { get; set; }

    [JsonProperty("accuracy")]
    public Accuracy[] Accuracy { get; set; }
}