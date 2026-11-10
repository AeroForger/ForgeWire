using System.Diagnostics;
using System.Net.Http;
using Spectre.Console;

namespace ForgeWire.TTS;

/// <summary>
/// Piper neural TTS: near-human quality, runs locally.
/// Binary lives in appdata/piper, voices in appdata/voices (*.onnx + *.onnx.json).
/// </summary>
public static class Piper
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(15),
    };

    private static string BaseDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ForgeWire");

    public static string PiperDir => Path.Combine(BaseDir, "piper");
    public static string VoicesDir => Path.Combine(BaseDir, "voices");

    private static string PiperBin => Path.Combine(PiperDir, "piper");

    public static bool IsInstalled() =>
        File.Exists(PiperBin) && File.Exists(
            Path.Combine(PiperDir, "libonnxruntime.so"));

    public static List<string> ListModels()
    {
        try
        {
            if (!Directory.Exists(VoicesDir)) return new();
            return Directory.GetFiles(VoicesDir, "*.onnx")
                .Select(Path.GetFileName)
                .Where(f => File.Exists(Path.Combine(VoicesDir, f + ".json")))
                .Cast<string>()
                .OrderBy(f => f)
                .ToList();
        }
        catch { return new(); }
    }

    /// <summary>Curated Piper voices (all verified URLs).</summary>
    public static readonly (string Label, string RelDir, string File)[] Catalogue =
    {
        ("Amy    - American female, warm and natural",      "en/en_US/amy/medium",   "en_US-amy-medium"),
        ("Ryan   - American male, news-anchor style",       "en/en_US/ryan/high",    "en_US-ryan-high"),
        ("Lessac - American female, high quality",          "en/en_US/lessac/high",  "en_US-lessac-high"),
        ("Alba   - British female, clear",                  "en/en_GB/alba/medium",  "en_GB-alba-medium"),
        ("Joe    - American male, casual",                  "en/en_US/joe/medium",   "en_US-joe-medium"),
        ("Kathleen - American female, small & fast model",  "en/en_US/kathleen/low", "en_US-kathleen-low"),
        ("Northern English Male - British (Geordie-ish)",   "en/en_GB/northern_english_male/medium", "en_GB-northern_english_male-medium"),
    };

    public static async Task<bool> InstallBinaryAsync()
    {
        try
        {
            Directory.CreateDirectory(PiperDir);
            string tmp = Path.Combine(Path.GetTempPath(),
                $"forgewire_piper_{Guid.NewGuid():N}");
            string tmpDir = Path.Combine(tmp, "x");
            Directory.CreateDirectory(tmpDir);

            string tarball = Path.Combine(tmp, "piper.tar.gz");
            byte[] bytes = await Http.GetByteArrayAsync(
                "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_linux_x86_64.tar.gz");
            await File.WriteAllBytesAsync(tarball, bytes);

            // Extract with system tar, then copy contents into PiperDir.
            var tar = Process.Start(new ProcessStartInfo
            {
                FileName = "tar",
                ArgumentList = { "-xzf", tarball, "-C", tmpDir },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            tar?.WaitForExit();

            string extracted = Path.Combine(tmpDir, "piper");
            if (!Directory.Exists(extracted)) return false;

            foreach (var f in Directory.GetFiles(extracted))
            {
                string dest = Path.Combine(PiperDir, Path.GetFileName(f));
                File.Copy(f, dest, true);
            }
            try { Directory.Delete(tmp, true); } catch { }
            return IsInstalled();
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> DownloadVoiceAsync(
        string relDir, string file, Action<string>? progress = null)
    {
        try
        {
            Directory.CreateDirectory(VoicesDir);
            string baseUrl =
                $"https://huggingface.co/rhasspy/piper-voices/resolve/main/{relDir}/{file}";

            progress?.Invoke($"Downloading {file}.onnx (this can take a minute)...");
            byte[] model = await Http.GetByteArrayAsync(baseUrl + ".onnx");
            await File.WriteAllBytesAsync(
                Path.Combine(VoicesDir, file + ".onnx"), model);

            progress?.Invoke($"Downloading {file}.onnx.json...");
            byte[] cfg = await Http.GetByteArrayAsync(baseUrl + ".onnx.json");
            await File.WriteAllBytesAsync(
                Path.Combine(VoicesDir, file + ".onnx.json"), cfg);

            return true;
        }
        catch (Exception ex)
        {
            progress?.Invoke($"Download failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates a WAV for the given text with the selected Piper model.
    /// </summary>
    public static byte[]? Generate(string text)
    {
        var settings = VoiceSettings.Current;

        string modelPath = Path.Combine(VoicesDir, settings.PiperModel);
        if (!File.Exists(modelPath))
        {
            AnsiConsole.MarkupLine(
                "[bold red]No Piper voice found.[/]");
            AnsiConsole.MarkupLine(
                "[grey]Pick one in Settings > TTS Options > Switch TTS engine.[/]");
            return null;
        }

        string outFile = Path.Combine(
            Path.GetTempPath(), $"forgewire_piper_{Guid.NewGuid():N}.wav");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = PiperBin,
                ArgumentList = { "--model", modelPath, "--output_file", outFile },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // piper's bundled onnxruntime/espeak libs sit next to the binary.
            psi.Environment["LD_LIBRARY_PATH"] = PiperDir;

            using var p = Process.Start(psi);
            if (p == null) return null;
            p.StandardInput.Write(text);
            p.StandardInput.Flush();
            p.StandardInput.Close();
            p.WaitForExit();

            if (p.ExitCode != 0 || !File.Exists(outFile) ||
                new FileInfo(outFile).Length == 0)
            {
                string err = p.StandardError.ReadToEnd();
                AnsiConsole.MarkupLine(
                    $"[bold red]piper failed (exit {p.ExitCode}):[/] " +
                    $"[grey]{Spectre.Console.Markup.Escape(err.Split('\n').LastOrDefault(l => l.Trim().Length > 0) ?? "")}[/]");
                return null;
            }

            return File.ReadAllBytes(outFile);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[bold red]Error running piper: {Spectre.Console.Markup.Escape(ex.Message)}[/]");
            return null;
        }
        finally
        {
            try { File.Delete(outFile); } catch { }
        }
    }
}
