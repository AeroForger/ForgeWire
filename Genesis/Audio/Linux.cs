using System;
using System.Diagnostics;
using System.IO;

namespace ForgeWire.Audio;

class LinuxVirtualMic
{
    private static Process? _virtualMicProcess;
    private static string? _pulseModuleId;
    private static bool _initialized;

    public static void SendToVirtualMic(byte[] wavBytes)
    {
        try
        {
            // Initialize the virtual microphone only once.
            if (!_initialized)
            {
                InitializeVirtualMic();
            }
            // Play the WAV through the virtual microphone.
            using var aplayProcess = new Process();
            aplayProcess.StartInfo.FileName = "aplay";
            aplayProcess.StartInfo.Arguments = "-D Virtual_Mic";
            aplayProcess.StartInfo.UseShellExecute = false;
            aplayProcess.StartInfo.RedirectStandardInput = true;
            aplayProcess.StartInfo.CreateNoWindow = true;
            aplayProcess.Start();
            using (BinaryWriter writer =
                   new BinaryWriter(aplayProcess.StandardInput.BaseStream))
            {
                writer.Write(wavBytes);
                writer.Flush();
            }
            aplayProcess.WaitForExit();

            if (aplayProcess.ExitCode != 0)
            {
                Console.WriteLine(
                    $"aplay failed with exit code {aplayProcess.ExitCode}.");
                return;
            }

            Console.WriteLine("Audio successfully streamed to virtual mic.");
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
            "Detected PipeWire server. Creating node: Virtual_Mic");

        _virtualMicProcess = new Process();

        _virtualMicProcess.StartInfo.FileName = "pw-loopback";
        _virtualMicProcess.StartInfo.Arguments =
            "-m '[ FL FR ]' " +
            "--capture-props=" +
            "'media.class=Audio/Source " +
            "node.name=Virtual_Mic " +
            "node.description=\"Virtual Microphone\"'";

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
