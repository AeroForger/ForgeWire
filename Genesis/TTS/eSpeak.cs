using System.Diagnostics;
using Spectre.Console;
using ForgeWire.TTS;

class Convertor
{
    Process process = new Process();
    public byte[]? Convert(string text)
    {
        AnsiConsole.MarkupLine("[bold red]generating audio[/]");
        byte[]? audioData = GetVoiceBytes(text);
        if (audioData != null && audioData.Length > 0)
        {
            AnsiConsole.MarkupLine($"Generated {audioData.Length} bytes of raw audio stream.");

            AnsiConsole.MarkupLine("Step 2: Pumping byte array into Virtual Microphone...");
            return audioData;
        }
        else
        {
            AnsiConsole.MarkupLine("[bold red]Failed to generate audio bytes.[/]");
            return null;
        }

    }
    private byte[]? GetVoiceBytes(string text)
    {
        return Generate(text);
    }

    /// <summary>
    /// Generates a complete RIFF WAV of the given text using the
    /// current user settings. Does NOT open the virtual mic.
    /// </summary>
    public byte[]? Generate(string text)
    {
        var settings = VoiceSettings.Current;

        if (settings.TtsEngine == "piper")
        {
            return Piper.Generate(text);
        }
        return GenerateEspeak(text);
    }

    private byte[]? GenerateEspeak(string text)
    {
        try
        {
            var settings = VoiceSettings.Current;

            using (Process espeakProcess = new Process())
            {
                espeakProcess.StartInfo.FileName = "espeak-ng";
                // --stdout generates pure RIFF WAV format
                // A variant is appended to the voice: "en-us+Annie"
                string voice = string.IsNullOrWhiteSpace(settings.Variant)
                    ? settings.Voice
                    : $"{settings.Voice}+{settings.Variant}";
                espeakProcess.StartInfo.ArgumentList.Add("-v");
                espeakProcess.StartInfo.ArgumentList.Add(voice);
                espeakProcess.StartInfo.ArgumentList.Add("-s");
                espeakProcess.StartInfo.ArgumentList.Add(settings.Speed.ToString());
                espeakProcess.StartInfo.ArgumentList.Add("-p");
                espeakProcess.StartInfo.ArgumentList.Add(settings.Pitch.ToString());
                espeakProcess.StartInfo.ArgumentList.Add("-a");
                espeakProcess.StartInfo.ArgumentList.Add(settings.Volume.ToString());
                espeakProcess.StartInfo.ArgumentList.Add("-g");
                espeakProcess.StartInfo.ArgumentList.Add(settings.WordGap.ToString());
                espeakProcess.StartInfo.ArgumentList.Add("--stdout");
                espeakProcess.StartInfo.ArgumentList.Add(text);

                espeakProcess.StartInfo.UseShellExecute = false;
                espeakProcess.StartInfo.RedirectStandardError = true;
                espeakProcess.StartInfo.RedirectStandardOutput = true;
                espeakProcess.StartInfo.CreateNoWindow = true;

                espeakProcess.Start();

                using (MemoryStream ms = new MemoryStream())
                {
                    espeakProcess.StandardOutput.BaseStream.CopyTo(ms);
                    espeakProcess.WaitForExit();
                    if (espeakProcess.ExitCode != 0 || ms.Length == 0)
                    {
                        string err = espeakProcess.StandardError.ReadToEnd().Trim();
                        AnsiConsole.MarkupLine(
                            $"[bold red]espeak-ng failed (exit {espeakProcess.ExitCode}).[/]");
                        if (err.Length > 0)
                        {
                            AnsiConsole.MarkupLine(
                                $"[grey]{Spectre.Console.Markup.Escape(err)}[/]");
                        }
                        return null;
                    }
                    return ms.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[bold red]Error capturing eSpeak: {Spectre.Console.Markup.Escape(ex.Message)}[/]");
            return null;
        }
    }

    /// <summary>
    /// Plays generated audio through the DEFAULT speaker (for
    /// previewing voices in settings, not the virtual mic).
    /// </summary>
    public static void Preview(byte[] wavBytes)
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"forgewire_preview_{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(tempFile, wavBytes);

            using var playProcess = new Process();
            playProcess.StartInfo.FileName = "pw-cat";
            playProcess.StartInfo.ArgumentList.Add("--playback");
            playProcess.StartInfo.ArgumentList.Add(tempFile);
            playProcess.StartInfo.UseShellExecute = false;
            playProcess.StartInfo.CreateNoWindow = true;
            playProcess.Start();
            playProcess.WaitForExit();
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* ignore */ }
        }
    }
}
