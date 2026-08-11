using System.Diagnostics;
using Spectre.Console;
class Convertor
{
    Process process = new Process();
    public byte[]? Convert(string text)
    {
        AnsiConsole.MarkupLine("[bold red]generating audio");
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
        try
        {
            using (Process espeakProcess = new Process())
            {
                espeakProcess.StartInfo.FileName = "espeak-ng";
                // --stdout generates pure RIFF WAV format
                espeakProcess.StartInfo.Arguments = $"-v en-us -s 160 --stdout \"{text}\"";
                
                espeakProcess.StartInfo.UseShellExecute = false;
                espeakProcess.StartInfo.RedirectStandardOutput = true; 
                espeakProcess.StartInfo.CreateNoWindow = true;

                espeakProcess.Start();

                using (MemoryStream ms = new MemoryStream())
                {
                    espeakProcess.StandardOutput.BaseStream.CopyTo(ms);
                    espeakProcess.WaitForExit();
                    return ms.ToArray(); 
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"Error capturing eSpeak: {ex.Message}");
            return null;
        }
    }
}