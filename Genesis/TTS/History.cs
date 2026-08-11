using System;
using System.IO;
using Spectre.Console;

namespace ForgeWire.TTS;
class History
{
    string path = "History.txt";
    public void Save(string input, bool dataSaving)
    {
        if (!dataSaving)
        {
            AnsiConsole.MarkupLine("[bold red]Skipping data saving[/]");
            return;
        }

        File.AppendAllText(path, input + Environment.NewLine);
    }

    public string Load()
    {
        if (!File.Exists(path))
        {
            return "Empty";
        }

        string history = File.ReadAllText(path);

        return string.IsNullOrWhiteSpace(history) ? "Empty" : history;
    }
}