using System.Text.Json;

namespace ForgeWire.TTS;

/// <summary>
/// Persistent voice/TTS configuration, stored in ~/.config/ForgeWire/settings.json
/// </summary>
public sealed class VoiceSettings
{
    // Selected voice/language code, e.g. "en-us", "en-gb"
    public string Voice { get; set; } = "en-us";

    // Voice variant name (file in espeak-ng-data/voices/!v), empty = none
    public string Variant { get; set; } = "";

    // -s words per minute (80-450)
    public int Speed { get; set; } = 165;

    // -p pitch 0-99 (50 = default)
    public int Pitch { get; set; } = 50;

    // -a amplitude/volume 0-200 (100 = default)
    public int Volume { get; set; } = 100;

    // -g word gap, units of 10ms
    public int WordGap { get; set; } = 10;

    // TTS engine: "espeak" or "piper"
    public string TtsEngine { get; set; } = "espeak";

    // Piper: file name of the selected .onnx model inside the voices dir
    public string PiperModel { get; set; } = "en_US-amy-medium.onnx";

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Variant)
            ? Voice
            : $"{Voice} +{Variant}";

    private static VoiceSettings? _current;

    public static VoiceSettings Current => _current ??= Load();

    private static string ConfigPath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ForgeWire");
            return Path.Combine(dir, "settings.json");
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Config saving is best-effort; never crash over it.
        }
    }

    public static VoiceSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<VoiceSettings>(
                    File.ReadAllText(ConfigPath));
                if (loaded != null)
                {
                    _current = loaded;
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults.
        }

        var settings = new VoiceSettings();
        settings.Save();
        return settings;
    }
}
