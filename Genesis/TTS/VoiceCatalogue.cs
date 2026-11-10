using System.Diagnostics;

namespace ForgeWire.TTS;

/// <summary>
/// Curated "realistic" voice presets plus helpers that enumerate
/// every language and variant the installed espeak-ng supports.
/// </summary>
public static class VoiceCatalogue
{
    public sealed record Preset(
        string Name, string Voice, string Variant,
        int Speed, int Pitch, int Volume, int WordGap);

    // Klatt variants resynthesize speech with a physical vocal-tract
    // model which sounds noticeably more human than espeak's default.
    public static readonly Preset[] Presets =
    {
        new("Tyler  - natural American male (Klatt)",   "en-us", "klatt4",    165, 55, 110, 8),
        new("Annie  - young American female",           "en-us", "Annie",     160, 55, 110, 8),
        new("Benjamin - deep British gentleman",        "en-gb", "benjamin",  145, 35, 115, 12),
        new("Alex   - newsreader American male",        "en-us", "Alex",      170, 50, 105, 6),
        new("Alicia - warm American female",            "en-us", "Alicia",    160, 60, 105, 8),
        new("Announcer - radio DJ energy",              "en-us", "announcer", 175, 50, 120, 5),
        new("Reed   - calm soft male (Klatt)",          "en-us", "klatt2",    155, 45,  95, 10),
        new("Whisper - low airy voice",                 "en-us", "whisper",   150, 45,  85, 12),
        new("Grandpa - old man",                        "en-us", "grandpa",   130, 30, 100, 15),
        new("Classic - the plain robot (default)",      "en-us", "",          160, 50, 100, 10),
    };

    public static void Apply(Preset p)
    {
        var s = VoiceSettings.Current;
        s.Voice = p.Voice;
        s.Variant = p.Variant;
        s.Speed = p.Speed;
        s.Pitch = p.Pitch;
        s.Volume = p.Volume;
        s.WordGap = p.WordGap;
        s.Save();
    }

    /// <summary>All voice variants shipped with the installed espeak-ng.</summary>
    public static List<string> ListVariants()
    {
        var result = new List<string>();
        try
        {
            string dir = Path.Combine(
                "/usr/share/espeak-ng-data/voices", "!v");
            if (Directory.Exists(dir))
            {
                result = Directory.GetFiles(dir)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Cast<string>()
                    .OrderBy(f => f)
                    .ToList();
            }
        }
        catch { /* best-effort */ }
        return result;
    }

    /// <summary>
    /// Every installed voice/language as (code, friendly name).
    /// </summary>
    public static List<(string Code, string Name)> ListLanguages()
    {
        var result = new List<(string, string)>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "espeak-ng",
                ArgumentList = { "--voices" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return result;
            while (!p.StandardOutput.EndOfStream)
            {
                string line = p.StandardOutput.ReadLine() ?? "";
                if (!line.Contains('/') || line.StartsWith("Pty")) continue;
                var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length >= 5)
                {
                    result.Add((cols[2], cols[4]));
                }
            }
            p.WaitForExit();
        }
        catch { /* best-effort */ }
        return result;
    }
}
