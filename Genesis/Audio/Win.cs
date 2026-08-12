using System.Diagnostics;
using NAudio.Wave;
using Spectre.Console;

namespace ForgeWire.Audio;

class WindowsVirtualMic
{
    public static void SendToMic(byte[] wavBytes)
    {
        int deviceNumber = MicInit();

        if (deviceNumber == -1)
        {
            Console.WriteLine("Couldn't find the virtual device.");
            return;
        }

        using var stream = new MemoryStream(wavBytes);
        using var reader = new WaveFileReader(stream);
        using var outputDevice = new WaveOutEvent
        {
            DeviceNumber = deviceNumber
        };

        outputDevice.Init(reader);
        outputDevice.Play();

        while (outputDevice.PlaybackState == PlaybackState.Playing)
        {
            Thread.Sleep(50);
        }
    }
    private static int MicInit()
    {
        for (int n = 0; n < WaveOut.DeviceCount; n++)
        {
            var capabilities = WaveOut.GetCapabilities(n);

            if (capabilities.ProductName.Contains("VB-Audio") ||
                capabilities.ProductName.Contains("CABLE Input"))
            {
                
                return n;
            }
        }

        return -1;
    }
}
