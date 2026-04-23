using System.Text.Json.Serialization;

namespace RetakesPlugin.Configs;

public class BombSettings
{
    [JsonPropertyName("IsAutoPlantEnabled")]
    public bool IsAutoPlantEnabled { get; set; } = true;

    [JsonPropertyName("IsInstantPlantEnabled")]
    public bool IsInstantPlantEnabled { get; set; } = true;

    [JsonPropertyName("IsInstantDefuseEnabled")]
    public bool IsInstantDefuseEnabled { get; set; } = true;

    [JsonPropertyName("InstantDefuseThreatRadius")]
    public float InstantDefuseThreatRadius { get; set; } = 250.0f;
}
