using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Centerly.Core.AutoCenter;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;

namespace Centerly.Core.Settings;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    Converters = [typeof(HotkeyJsonConverter)])]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(OversizeBehavior))]
[JsonSerializable(typeof(AppLanguage))]
[JsonSerializable(typeof(AutoCenterMode))]
[JsonSerializable(typeof(RuleAction))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Context with relaxed escaping so the file stays readable: "+" in hotkeys and Cyrillic are written as is, not as \uXXXX.
    /// Safe here, the JSON never ends up in HTML.
    /// </summary>
    // Lazy: static initializers across partial declarations run in unspecified order, Default may still be null.
    public static SettingsJsonContext Readable => field ??= new(new JsonSerializerOptions(Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}

/// <summary>Stores hotkeys as readable strings ("Ctrl+Alt+C"), empty string for none.</summary>
internal sealed class HotkeyJsonConverter : JsonConverter<Hotkey>
{
    public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return Hotkey.None;

        return Hotkey.TryParse(text, out var hotkey)
            ? hotkey.Value
            : throw new JsonException($"Invalid hotkey '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
