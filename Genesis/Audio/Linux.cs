using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ForgeWire.Audio;

class LinuxVirtualMic
{
    private static Process? _virtualMicProcess = null;

    public static void SendToVirtualMic(byte[] wavBytes)
    {
        try
        {
            // 1. Automatically spin up the virtual device if it isn't running yet
            if (_virtualMicProcess == null || _virtualMicProcess.HasExited)
            {
                InitializeVirtualMic();
            }

            // 2. Play the bytes using the setup virtual audio node
            using (Process aplayProcess = new Process())
            {
                aplayProcess.StartInfo.FileName = "aplay";
                aplayProcess.StartInfo.Arguments = "-D Virtual_Mic";
                aplayProcess.StartInfo.UseShellExecute = false;
                aplayProcess.StartInfo.RedirectStandardInput = true;
                aplayProcess.StartInfo.CreateNoWindow = true;

                aplayProcess.Start();

                using (BinaryWriter writer = new BinaryWriter(aplayProcess.StandardInput.BaseStream))
                {
                    writer.Write(wavBytes);
                    writer.Flush();
                }

                aplayProcess.WaitForExit();
                Console.WriteLine("Audio successfully streamed to virtual mic.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending audio to virtual mic: {ex.Message}");
        }
    }

    private static void InitializeVirtualMic()
    {
        Console.WriteLine("Initializing virtual microphone device...");

        bool isPipeWire = File.Exists("/usr/bin/pw-loopback") || File.Exists("/bin/pw-loopback");

        _virtualMicProcess = new Process();
        _virtualMicProcess.StartInfo.UseShellExecute = false;
        _virtualMicProcess.StartInfo.CreateNoWindow = true;

        if (isPipeWire)
        {
            // PipeWire Configuration
            _virtualMicProcess.StartInfo.FileName = "pw-loopback";
            _virtualMicProcess.StartInfo.Arguments = "-m '[ FL FR ]' --capture-props='media.class=Audio/Source node.name=Virtual_Mic node.description=\"Virtual Microphone\"'";
            Console.WriteLine("Detected PipeWire server. Creating node: Virtual_Mic");
        }
        else
        {
            // PulseAudio Configuration (Fallback)
            _virtualMicProcess.StartInfo.FileName = "pactl";
            _virtualMicProcess.StartInfo.Arguments = "load-module module-null-sink sink_name=Virtual_Mic sink_properties=device.description=\"Virtual_Microphone\"";
            Console.WriteLine("Detected PulseAudio server. Creating node: Virtual_Mic");
        }

        _virtualMicProcess.Start();

        // Give the OS kernel 500 milliseconds to register the new hardware mapping
        Thread.Sleep(500); 
        
        // Register an exit cleanup loop to destroy the virtual mic when your program closes
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => CleanupVirtualMic();
    }

    private static void CleanupVirtualMic()
    {
        if (_virtualMicProcess != null && !_virtualMicProcess.HasExited)
        {
            Console.WriteLine("\nCleaning up virtual mic audio nodes...");
            try
            {
                _virtualMicProcess.Kill();
                _virtualMicProcess.Dispose();
            }
            catch { /* Suppress exit errors */ }
        }
    }
}
