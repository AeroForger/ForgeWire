using System;
using Spectre.Console;

namespace ForgeWire.Engine;

class Menu
{
    public string StartMenu()
    {
        Console.Clear();
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]ForgeWire[/] [grey]<v1.0>[/]")
                .PageSize(8)
                .HighlightStyle(Style.Parse("bold yellow"))
                .MoreChoicesText("[grey](Use arrow keys to navigate, Enter to select)[/]")
                .AddChoices(new[] {
                    "Start ForgeWire",
                    "History",
                    "Information",
                    "Settings",
                    "Exit"
                })
        );
    }
    public string Settings()
    {
        Console.Clear();
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Settings are going to be added[/]")
                .PageSize(10)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(new[] {
                    "TTS Options",
                    "Data Saving",
                    "Go back"
                })
        );
    }
    public string DataOptions()
    {
        Console.Clear();
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Settings are going to be added[/]")
                .PageSize(10)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(new[] {
                    "ON",
                    "OFF"
                })
        );
    }
    public void TtsOptions()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold red]This page is currently in development");
        AnsiConsole.MarkupLine("Press any key to exit back");
        Console.ReadKey();
        return;
    }

    public string InputField()
    {
        Console.Clear();
        return AnsiConsole.Ask<string>("[bold blue]Type[/] your desired text: ");
    }
    public void Info()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold green]ForgeWire[/] is an cli app that [bold yellow]converts text to speech that outputs to a virtual mic[/]");
        AnsiConsole.MarkupLine("[bold red]Press any button to go back[/]");
        Console.ReadKey();
        return;
    }
}