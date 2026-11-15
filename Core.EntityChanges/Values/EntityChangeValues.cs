using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

/// <summary>One property of a change row. A masked one (a secret) carries no value, only the fact that it changed.</summary>
public sealed record PropertyChange(string Property, string? OldValue, string? NewValue, bool Masked = false);

/// <summary>
/// Change log values as language-neutral text (the screen formats them), each cut at 512 characters, and the JSON a
/// row stores them in: <c>[{"p","o","n"}]</c>, a secret as <c>{"p","m":true}</c>. The JSON shape is a contract with
/// the rows already stored; changing it is a breaking change.
/// </summary>
public static class EntityChangeValues
{
    public const int MaxValueLength = 512;
    public const int MaxEntityIdLength = 128;
    private const string Ellipsis = "…";
    private const string SetSeparator = ", ";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        // Keeps accented letters readable in exports; HTML-sensitive characters stay escaped.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private sealed class StoredChange
    {
        [JsonPropertyName("p")] public string P { get; set; } = string.Empty;
        [JsonPropertyName("o")] public string? O { get; set; }
        [JsonPropertyName("n")] public string? N { get; set; }
        [JsonPropertyName("m")] public bool M { get; set; }
    }

    public static string? Format(object? value) => Cut(value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "true" : "false",
        Enum member => member.ToString(),
        DateTime time => DateTime.SpecifyKind(time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time, DateTimeKind.Utc)
            .ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        Guid id => id.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    });

    /// <summary>A relation's members as one value: names sorted, comma separated; an empty set is null.</summary>
    public static string? SetText(IEnumerable<string> items)
    {
        string[] sorted = items.Order(StringComparer.Ordinal).ToArray();
        return sorted.Length == 0 ? null : Cut(string.Join(SetSeparator, sorted));
    }

    public static string Serialize(IEnumerable<PropertyChange> changes) =>
        JsonSerializer.Serialize(changes.Select(c => c.Masked
            ? new StoredChange { P = c.Property, M = true }
            : new StoredChange { P = c.Property, O = c.OldValue, N = c.NewValue }), JsonOptions);

    public static IReadOnlyList<PropertyChange> Deserialize(string json) =>
        (JsonSerializer.Deserialize<List<StoredChange>>(json, JsonOptions) ?? [])
        .Select(c => new PropertyChange(c.P, c.O, c.N, c.M))
        .ToList();

    private static string? Cut(string? value) =>
        value is null || value.Length <= MaxValueLength ? value : value[..(MaxValueLength - Ellipsis.Length)] + Ellipsis;
}
