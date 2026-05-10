using Spectre.Console;

namespace UbuntuSafeSnap.UI;

public static class ConsolePrompt
{
    public static bool IsInteractive => AnsiConsole.Console.Profile.Capabilities.Interactive;

    public static string PromptSelection(string title, IEnumerable<string> choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(choices)
        );
    }

    public static bool Confirm(string prompt)
    {
        return AnsiConsole.Confirm(prompt);
    }

    public static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}