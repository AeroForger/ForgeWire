using System;
using System.Diagnostics;
using System.IO;

namespace ForgeWire.Audio;

class LinuxVirtualMic
{
    private static Process? _virtualMicProcess;
    private static string? _pulseModuleId;
    private static bool _initialized;

    /// <summary>
    /// Creates the virtual mic nodes immediately, so apps like Discord
    /// can see the device without having to send audio first.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (!_initialized)
        {
            InitializeVirtualMic();
        }
    }

    public static void SendToVirtualMic(byte[] wavBytes)
    {
        try
        {
            // Initialize the virtual microphone only once.
            if (!_initialized)
            {
                InitializeVirtualMic();
            }

            // pw-cat can't reliably read a WAV header from stdin, so write
            // the WAV to a temp file and play it into the virtual sink.
            string tempFile = Path.Combine(
                Path.GetTempPath(), $"forgewire_{Guid.NewGuid():N}.wav");
            try
            {
                File.WriteAllBytes(tempFile, wavBytes);

                using var playProcess = new Process();
                playProcess.StartInfo.FileName = "pw-cat";
                playProcess.StartInfo.ArgumentList.Add("--playback");
                playProcess.StartInfo.ArgumentList.Add("--target=VirtualMic");
                playProcess.StartInfo.ArgumentList.Add(tempFile);
                playProcess.StartInfo.UseShellExecute = false;
                playProcess.StartInfo.CreateNoWindow = true;
                playProcess.Start();
                playProcess.WaitForExit();

                if (playProcess.ExitCode != 0)
                {
                    Console.WriteLine(
                        $"pw-cat failed with exit code {playProcess.ExitCode}.");
                    return;
                }

                Console.WriteLine("Audio successfully streamed to virtual mic.");
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error sending audio to virtual mic: {ex.Message}");
        }
    }

    private static void InitializeVirtualMic()
    {
        Console.WriteLine("Initializing virtual microphone device...");

        if (CheckIsPipeWire())
        {
            InitializePipeWire();
        }
        else
        {
            InitializePulseAudio();
        }

        _initialized = true;

        AppDomain.CurrentDomain.ProcessExit +=
            (_, _) => CleanupVirtualMic();
    }




    private static void InitializePipeWire()
    {
        Console.WriteLine(
            "Detected PipeWire server. Creating virtual mic nodes...");

        _virtualMicProcess = new Process();

        _virtualMicProcess.StartInfo.FileName = "pw-loopback";
        _virtualMicProcess.StartInfo.ArgumentList.Add("-m");
        _virtualMicProcess.StartInfo.ArgumentList.Add("[ FL FR ]");
        // Capture side: a virtual SINK we can play audio into.
        _virtualMicProcess.StartInfo.ArgumentList.Add(
            "--capture-props=" +
            "media.class=Audio/Sink node.name=VirtualMic " +
            "node.description=\"Virtual Microphone (input)\"");
        // Playback side: a virtual SOURCE other apps can record from.
        _virtualMicProcess.StartInfo.ArgumentList.Add(
            "--playback-props=" +
            "media.class=Audio/Source node.name=Virtual_Mic " +
            "node.description=\"Virtual Microphone\"");

        _virtualMicProcess.StartInfo.UseShellExecute = false;
        _virtualMicProcess.StartInfo.CreateNoWindow = true;

        _virtualMicProcess.Start();
    }

    private static void InitializePulseAudio()
    {
        Console.WriteLine(
            "Detected PulseAudio server. Creating node: Virtual_Mic");

        using var process = new Process();
        process.StartInfo.FileName = "pactl";
        process.StartInfo.Arguments =
            "load-module " +
            "module-remap-source " +
            "master=@DEFAULT_SINK@.monitor " +
            "source_name=Virtual_Mic " +
            "source_properties=device.description=\"Virtual_Microphone\"";

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();

        // pactl prints the module ID to stdout.
        _pulseModuleId =
            process.StandardOutput.ReadToEnd().Trim();

        string error =
            process.StandardError.ReadToEnd().Trim();

        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(_pulseModuleId))
        {
            throw new Exception(
                $"Failed to create PulseAudio virtual microphone. {error}");
        }

        Console.WriteLine(
            $"Created PulseAudio module: {_pulseModuleId}");
    }

    private static void CleanupVirtualMic()
    {
        try
        {
            Console.WriteLine(
                "\nCleaning up virtual microphone...");
            // PipeWire
            if (_virtualMicProcess != null)
            {
                if (!_virtualMicProcess.HasExited)
                {
                    _virtualMicProcess.Kill();
                }

                _virtualMicProcess.Dispose();
                _virtualMicProcess = null;
            }

            // PulseAudio
            if (!string.IsNullOrWhiteSpace(_pulseModuleId))
            {
                using var process = new Process();

                process.StartInfo.FileName = "pactl";
                process.StartInfo.Arguments =
                    $"unload-module {_pulseModuleId}";

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                process.WaitForExit();

                _pulseModuleId = null;
            }

            _initialized = false;
        }
        catch
        {
            // Don't let cleanup errors crash the application.
        }
    }

    private static bool CheckIsPipeWire()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pactl",
                Arguments = "info",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                return false;
            }

            string output =
                process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return output.Contains(
                "pipewire",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // pactl isn't available or isn't usable.
            return false;
        }
    }
}
