using System.Text.Json.Serialization;

namespace HuntStats.Models;

public class Dataset
{
    [JsonPropertyName("label")]
    public string Label { get; set; }

    [JsonPropertyName("data")]
    public List<int> Data { get; set; }

    [JsonPropertyName("fill")]
    public bool Fill { get; set; }

    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; }

    [JsonPropertyName("tension")]
    public double Tension { get; set; }
}

