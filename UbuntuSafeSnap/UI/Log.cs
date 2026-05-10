using Spectre.Console;

namespace UbuntuSafeSnap.UI;

public static class Log
{
    public static void Info(string service, string message)
    {
        AnsiConsole.MarkupLine($"[grey][[{service}]] [/]{ConsolePrompt.EscapeMarkup(message)}");
    }

    public static void Error(string service, string message)
    {
        Console.Error.WriteLine($"[{service}] {message}");
    }
}