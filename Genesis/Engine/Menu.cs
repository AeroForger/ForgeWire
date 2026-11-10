using System;
using Spectre.Console;
using ForgeWire.TTS;

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
                .Title("[bold green]Settings[/]")
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
                .Title("[bold green]Data Saving[/] [grey]- saves every message to history[/]")
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
        var settings = VoiceSettings.Current;
        var convertor = new Convertor();

        while (true)
        {
            Console.Clear();
            AnsiConsole.Write(new Rule("[bold green]Voice / TTS Settings[/]").RuleStyle(Style.Parse("grey")));
            AnsiConsole.MarkupLine(
                $"[bold blue]Current engine:[/] [yellow]{(settings.TtsEngine == "piper" ? "Piper (neural)" : "eSpeak NG")}[/] " +
                $"[grey]|[/] [bold blue]voice:[/] [yellow]{Spectre.Console.Markup.Escape(settings.TtsEngine == "piper" ? settings.PiperModel : settings.DisplayName)}[/]");
            if (settings.TtsEngine == "espeak")
            {
                AnsiConsole.MarkupLine($"[grey]speed {settings.Speed} wpm | pitch {settings.Pitch}/99 | volume {settings.Volume}/200 | word gap {settings.WordGap}x10ms[/]");
            }
            AnsiConsole.WriteLine();

            string choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold green]What do you want to change?[/]")
                    .PageSize(10)
                    .HighlightStyle(Style.Parse("bold yellow"))
                    .AddChoices(new[] {
                        "Switch TTS engine",
                        "Pick a preset voice",
                        "Browse ALL voices & variants",
                        "Fine tuning (speed/pitch/volume)",
                        "Test voice (plays on your speakers)",
                        "Go back"
                    }));

            switch (choice)
            {
                case "Switch TTS engine":
                    EnginePicker(convertor);
                    break;
                case "Pick a preset voice":
                    PickPreset(convertor);
                    break;
                case "Browse ALL voices & variants":
                    CustomVoice(convertor);
                    break;
                case "Fine tuning (speed/pitch/volume)":
                    FineTune(convertor);
                    break;
                case "Test voice (plays on your speakers)":
                    TestVoice(convertor);
                    break;
                case "Go back":
                    return;
            }
        }
    }

    private async void EnginePicker(Convertor convertor)
    {
        var settings = VoiceSettings.Current;

        string engine = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Which TTS engine?[/]")
                .AddChoices(new[] {
                    "eSpeak NG  (instant, 10 preset voices, robotic-ish)",
                    "Piper  (neural, near-human quality, needs ~60MB voice)"
                }));

        if (engine.StartsWith("eSpeak"))
        {
            settings.TtsEngine = "espeak";
            settings.Save();
            AnsiConsole.MarkupLine("[green]Engine set to eSpeak NG.[/]");
            return;
        }

        // --- Piper ---
        if (!Piper.IsInstalled())
        {
            AnsiConsole.MarkupLine("[yellow]Piper is not installed yet (~10MB download).[/]");
            if (!AnsiConsole.Confirm("Download and install Piper now?", true))
            {
                return;
            }

            AnsiConsole.Status()
                .Start("Downloading Piper...", ctx =>
                {
                    bool ok = Piper.InstallBinaryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    ctx.Status(ok ? "Installed!" : "FAILED");
                    AnsiConsole.MarkupLine(ok
                        ? "[green]Piper installed.[/]"
                        : "[bold red]Piper installation failed.[/]");
                });
            if (!Piper.IsInstalled()) return;
        }

        if (!AnsiConsole.Confirm("Download a voice model? (Piper can't run without one)", true))
        {
            return;
        }

        // Show already-downloaded models first.
        string[] modelChoices = Piper.ListModels()
            .Select(m => $"Use downloaded: {m}")
            .ToArray();

        var allChoices = new List<string>(modelChoices);
        foreach (var v in Piper.Catalogue)
        {
            string fileName = v.File + ".onnx";
            if (!Piper.ListModels().Contains(fileName))
            {
                allChoices.Add($"Download: {v.Label}");
            }
        }

        string pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Piper voice[/] [grey](high quality models are bigger/slower)[/]")
                .PageSize(12)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(allChoices));

        if (pick.StartsWith("Use downloaded: "))
        {
            settings.TtsEngine = "piper";
            settings.PiperModel = pick.Substring("Use downloaded: ".Length);
            settings.Save();
        }
        else if (pick.StartsWith("Download: "))
        {
            var entry = Piper.Catalogue.FirstOrDefault(
                v => pick.Contains(v.Label));
            if (entry.File == null) return;

            bool ok = AnsiConsole.Confirm(
                $"Download '{Path.GetFileName(entry.File)}.onnx' now?", true);
            if (!ok) return;

            await Piper.DownloadVoiceAsync(
                entry.RelDir, entry.File,
                msg => AnsiConsole.MarkupLine(
                    $"[grey]{Spectre.Console.Markup.Escape(msg)}[/]"));

            if (Piper.ListModels().Contains(entry.File + ".onnx"))
            {
                settings.TtsEngine = "piper";
                settings.PiperModel = entry.File + ".onnx";
                settings.Save();
                AnsiConsole.MarkupLine("[green]Voice downloaded and selected.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[bold red]Voice download failed.[/]");
                return;
            }
        }
        else
        {
            return;
        }

        if (AnsiConsole.Confirm("Test Piper on your speakers now?", true))
        {
            TestVoice(convertor);
        }
    }

    private void PickPreset(Convertor convertor)
    {
        var names = VoiceCatalogue.Presets.Select(p => p.Name).ToList();
        names.Add("Go back");

        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Choose a preset voice[/]")
                .PageSize(12)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(names));

        var preset = VoiceCatalogue.Presets.FirstOrDefault(p => p.Name == choice);
        if (preset != null)
        {
            VoiceCatalogue.Apply(preset);
            AnsiConsole.MarkupLine($"[green]Voice set to:[/] [bold]{Spectre.Console.Markup.Escape(preset.Name)}[/]");
            PromptTestAfterPick(convertor);
        }
    }

    private void PromptTestAfterPick(Convertor convertor)
    {
        bool test = AnsiConsole.Confirm("Test this voice on your speakers now?");
        if (test)
        {
            TestVoice(convertor);
        }
    }

    private void CustomVoice(Convertor convertor)
    {
        var settings = VoiceSettings.Current;

        // --- Language ---
        bool all = AnsiConsole.Confirm("Show ALL languages? (No = English voices only)");
        var langs = VoiceCatalogue.ListLanguages();
        if (langs.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Could not list languages from espeak-ng.[/]");
            return;
        }
        if (!all)
        {
            langs = langs.Where(l => l.Code.StartsWith("en")).ToList();
        }

        string voice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Pick a language / voice[/]")
                .PageSize(15)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(langs.Select(l => $"{l.Code}  ({l.Name})")));

        settings.Voice = voice.Split(' ')[0];

        // --- Variant ---
        var variants = new List<string> { "(none - plain voice)" };
        variants.AddRange(VoiceCatalogue.ListVariants());

        string variant = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold green]Pick a voice variant (character)[/]")
                .PageSize(15)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(variants));

        settings.Variant = variant == "(none - plain voice)" ? "" : variant;
        settings.Save();

        AnsiConsole.MarkupLine($"[green]Voice set to:[/] [bold]{Spectre.Console.Markup.Escape(settings.DisplayName)}[/]");
        PromptTestAfterPick(convertor);
    }

    private void FineTune(Convertor convertor)
    {
        var settings = VoiceSettings.Current;

        settings.Speed = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold blue]Speed[/] in words per minute [grey](80-450, default 165)[/]:")
                .DefaultValue(settings.Speed)
                .Validate(v => v is >= 80 and <= 450
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 80-450[/]")));

        settings.Pitch = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold blue]Pitch[/] [grey](0-99, default 50)[/]:")
                .DefaultValue(settings.Pitch)
                .Validate(v => v is >= 0 and <= 99
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 0-99[/]")));

        settings.Volume = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold blue]Volume[/] [grey](0-200, default 100)[/]:")
                .DefaultValue(settings.Volume)
                .Validate(v => v is >= 0 and <= 200
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 0-200[/]")));

        settings.WordGap = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold blue]Word gap[/] in 10ms units [grey](0-50)[/]:")
                .DefaultValue(settings.WordGap)
                .Validate(v => v is >= 0 and <= 50
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Must be 0-50[/]")));

        settings.Save();
        AnsiConsole.MarkupLine("[green]Settings saved.[/]");
        PromptTestAfterPick(convertor);
    }

    private void TestVoice(Convertor convertor)
    {
        string text = AnsiConsole.Ask<string>(
            "[bold blue]Type[/] a test phrase [grey](Enter for default)[/]:",
            "Hello, this is ForgeWire testing the virtual microphone.");

        AnsiConsole.MarkupLine("[grey]Generating & playing on default speaker...[/]");
        var wav = convertor.Generate(text);
        if (wav != null)
        {
            Convertor.Preview(wav);
            AnsiConsole.MarkupLine("[green]Done.[/]");
        }
        AnsiConsole.MarkupLine("[grey]Press any key to continue[/]");
        Console.ReadKey(true);
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