using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NetCoreBackend.NArchitecture.Core.Payments.Masking;

/// <summary>
/// Makes a provider message safe to keep: a card number (13–19 digits passing Luhn, spaces and dashes allowed) is removed wherever it
/// appears, secret fields are starred, personal fields are moved out. JSON and form bodies are walked field by field; any other text is
/// only cleared of card numbers. Card data should never reach us (hosted payment pages); this is the net under that rule.
/// </summary>
public static partial class PaymentDataMasker
{
    public const string Starred = "***";
    public const string Removed = "[removed]";
    private const int MinCardDigits = 13;
    private const int MaxCardDigits = 19;

    public static MaskedPayload Mask(string? body, string? contentType, PaymentMaskingRules? rules = null)
    {
        rules ??= PaymentMaskingRules.Default;
        var personal = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(body))
            return new MaskedPayload(body, personal);

        string type = contentType?.Split(';')[0].Trim().ToLowerInvariant() ?? "";
        // A provider may label JSON as text: a body that looks like JSON is walked as JSON whatever its content type.
        if ((type.EndsWith("json") || body.TrimStart() is ['{', ..] or ['[', ..]) && TryParseJson(body) is JsonNode json)
        {
            MaskNode(json, "", rules, personal);
            return new MaskedPayload(json.ToJsonString(), personal);
        }
        if (type == "application/x-www-form-urlencoded")
            return new MaskedPayload(MaskForm(body, rules, personal), personal);
        return new MaskedPayload(RemoveCardNumbers(body), personal);
    }

    /// <summary>Fields of a form or a notification, masked the same way (the incoming message, before it is logged).</summary>
    public static MaskedPayload MaskFields(IReadOnlyDictionary<string, string> fields, PaymentMaskingRules? rules = null)
    {
        string form = string.Join("&", fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
        return Mask(form, "application/x-www-form-urlencoded", rules);
    }

    public static bool IsCardNumber(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= MinCardDigits and <= MaxCardDigits && value.All(c => char.IsDigit(c) || c is ' ' or '-') && PassesLuhn(digits);
    }

    private static JsonNode? TryParseJson(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void MaskNode(JsonNode node, string path, PaymentMaskingRules rules, Dictionary<string, string> personal)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (string key in obj.Select(p => p.Key).ToList())
                {
                    string childPath = path.Length == 0 ? key : $"{path}.{key}";
                    string name = PaymentMaskingRules.Normalize(key);
                    JsonNode? child = obj[key];
                    if (rules.SecretFields.Contains(name))
                        obj[key] = Starred;
                    else if (rules.PersonalFields.Contains(name) && child is JsonValue personalValue)
                    {
                        personal[childPath] = personalValue.ToString();
                        obj[key] = Starred;
                    }
                    else if (child is not null)
                        MaskChild(obj, key, child, childPath, rules, personal);
                }
                break;
            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                    if (array[i] is JsonNode item)
                    {
                        if (item is JsonValue value && IsCardNumber(value.ToString()))
                            array[i] = Removed;
                        else
                            MaskNode(item, $"{path}[{i}]", rules, personal);
                    }
                break;
        }
    }

    private static void MaskChild(JsonObject parent, string key, JsonNode child, string path, PaymentMaskingRules rules, Dictionary<string, string> personal)
    {
        if (child is JsonValue value)
        {
            string text = value.ToString();
            if (IsCardNumber(text))
                parent[key] = Removed;
            else if (value.GetValueKind() == JsonValueKind.String && CardInText().IsMatch(text))
                parent[key] = RemoveCardNumbers(text);
        }
        else
            MaskNode(child, path, rules, personal);
    }

    private static string MaskForm(string body, PaymentMaskingRules rules, Dictionary<string, string> personal)
    {
        var parts = new List<string>();
        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            string key = Uri.UnescapeDataString((equals < 0 ? pair : pair[..equals]).Replace('+', ' '));
            string value = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            string name = PaymentMaskingRules.Normalize(key);
            if (rules.SecretFields.Contains(name))
                value = Starred;
            else if (rules.PersonalFields.Contains(name))
            {
                personal[key] = value;
                value = Starred;
            }
            else
                value = IsCardNumber(value) ? Removed : RemoveCardNumbers(value);
            parts.Add($"{key}={value}");
        }
        return string.Join("&", parts);
    }

    private static string RemoveCardNumbers(string text) =>
        CardInText().Replace(text, match => PassesLuhn(new string(match.Value.Where(char.IsDigit).ToArray())) ? Removed : match.Value);

    private static bool PassesLuhn(string digits)
    {
        int sum = 0;
        bool doubleIt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int digit = digits[i] - '0';
            if (doubleIt && (digit *= 2) > 9)
                digit -= 9;
            sum += digit;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    // 13–19 digits, optionally grouped by single spaces or dashes.
    [GeneratedRegex(@"(?<!\d)\d(?:[ -]?\d){12,18}(?!\d)")]
    private static partial Regex CardInText();
}
