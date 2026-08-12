using Spectre.Console;
using ForgeWire.TTS;
using System;
using ForgeWire.Audio;
using System.Runtime.CompilerServices;
using static System.OperatingSystem;

namespace ForgeWire.Engine;

class Core
{
    Menu menu = new();
    History historyManager = new();
    Convertor convertor = new();

    public bool isRunning {get; set;}

    public bool isDataSaving = true;
    public void MainLoop()
    {
        isRunning = true;
        while (isRunning)
        {
            string choice = menu.StartMenu();
            switch (choice)
            {
                case "Start ForgeWire":
                    var inputted = menu.InputField();
                    historyManager.Save(inputted, isDataSaving);
                    var audioData = convertor.Convert(inputted);
                    if (audioData == null)
                    {
                        break;
                    }
                    string os = OS();
                    if (os == "Linux")
                    {
                        LinuxVirtualMic.SendToVirtualMic(audioData);
                    }
                    else if (os == "Windows")
                    {
                        WindowsVirtualMic.SendToMic(audioData);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("Unsupported os");
                    }
                    break;
                case "History":
                    var history = historyManager.Load();
                    AnsiConsole.MarkupLine($"[bold yellow]{history}[/]");
                    AnsiConsole.MarkupLine("[bold red]Press any key to go back[/]");
                    Console.ReadKey();
                    break;
                case "Information":
                    menu.Info();
                    break;
                case "Settings":
                    string inputMenu = menu.Settings();
                    switch (inputMenu)
                    {
                        case "TTS Options":
                            menu.TtsOptions();
                            break;
                        case "Data Saving":
                            string DataOption = menu.DataOptions();
                            if (DataOption == "OFF"){
                                isDataSaving = false;
                            }
                            else 
                            {
                                isDataSaving = true;
                            }
                            break;
                        case "Go back":
                            break;
                    }

                    break;
                case "Exit":
                    Stop();
                    break;
            }
        }
        
    }
    private void Stop()
    {
        AnsiConsole.Markup("[bold red]Exitting. Bye![/]");
        isRunning = false;
    }
    private string OS()
    {
        if (IsLinux())
        {
            return "Linux";
        }
        else if (IsWindows())
        {
            return "Windows";
        }
        else
        {
            return "unknown";
        }
    }

}
