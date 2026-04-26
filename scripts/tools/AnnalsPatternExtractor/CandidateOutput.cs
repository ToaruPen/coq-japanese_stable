using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QudJP.Tools.AnnalsPatternExtractor;

internal sealed class SlotEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("raw")]
    public string Raw { get; set; } = "";

    [JsonPropertyName("default")]
    public string Default { get; set; } = "";
}

internal sealed class CandidateEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("source_file")]
    public string SourceFile { get; set; } = "";

    [JsonPropertyName("annal_class")]
    public string AnnalClass { get; set; } = "";

    [JsonPropertyName("switch_case")]
    public string? SwitchCase { get; set; }

    [JsonPropertyName("event_property")]
    public string EventProperty { get; set; } = "";

    [JsonPropertyName("sample_source")]
    public string SampleSource { get; set; } = "";

    [JsonPropertyName("extracted_pattern")]
    public string ExtractedPattern { get; set; } = "";

    [JsonPropertyName("slots")]
    public List<SlotEntry> Slots { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("ja_template")]
    public string JaTemplate { get; set; } = "";

    [JsonPropertyName("review_notes")]
    public string ReviewNotes { get; set; } = "";

    [JsonPropertyName("route")]
    public string Route { get; set; } = "annals";

    [JsonPropertyName("en_template_hash")]
    public string EnTemplateHash { get; set; } = "";
}

internal sealed class CandidateDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1";

    [JsonPropertyName("candidates")]
    public List<CandidateEntry> Candidates { get; set; } = new();
}

internal static class HashHelper
{
    public static string ComputeEnTemplateHash(CandidateEntry candidate)
    {
        // canonical_json over a fixed-shape payload: pattern, slots, sample_source, event_property, switch_case
        var payload = new SortedDictionary<string, object?>(System.StringComparer.Ordinal)
        {
            ["extracted_pattern"] = candidate.ExtractedPattern,
            ["slots"] = candidate.Slots.Select(s => new SortedDictionary<string, object?>(System.StringComparer.Ordinal)
            {
                ["default"] = s.Default,
                ["index"] = (object?)s.Index,
                ["raw"] = s.Raw,
                ["type"] = s.Type,
            }).ToList(),
            ["sample_source"] = candidate.SampleSource,
            ["event_property"] = candidate.EventProperty,
            ["switch_case"] = candidate.SwitchCase,
        };
        var canonical = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var sb = new StringBuilder("sha256:");
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

internal static class CandidateWriter
{
    public static void WriteToFile(string path, CandidateDocument document)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var json = JsonSerializer.Serialize(document, options);
        File.WriteAllText(path, json + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
