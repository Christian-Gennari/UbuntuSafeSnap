using System.Security.Cryptography;
using Spectre.Console;
using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class ConflictResolverService : IConflictResolverService
{
    private const long MaxDiffFileSize = 1 * 1024 * 1024;

    public Task<ConflictResolution> ResolveAsync(string stagingFile, string destFile)
    {
        ArgumentNullException.ThrowIfNull(stagingFile);
        ArgumentNullException.ThrowIfNull(destFile);

        return ResolveCoreAsync(stagingFile, destFile);
    }

    private async Task<ConflictResolution> ResolveCoreAsync(string stagingFile, string destFile)
    {
        string stagingHash = await ComputeSha256Async(stagingFile);
        string destHash = await ComputeSha256Async(destFile);

        if (stagingHash == destHash)
        {
            AnsiConsole.MarkupLine($"[green][[ConflictResolver]][/] Files are identical, skipping: {EscapeMarkup(destFile)}");
            return ConflictResolution.Skip;
        }

        if (!AnsiConsole.Console.Profile.Capabilities.Interactive)
        {
            Console.Error.WriteLine($"[ConflictResolver] Conflict requires user input: {destFile}");
            Console.Error.WriteLine("[ConflictResolver] Re-run in an interactive terminal to resolve.");
            return ConflictResolution.Abort;
        }

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow][[ConflictResolver]][/] Conflict detected:");
            AnsiConsole.MarkupLine($"  [grey]Backup: [/] {EscapeMarkup(stagingFile)}");
            AnsiConsole.MarkupLine($"  [grey]System:[/] {EscapeMarkup(destFile)}");

            var choice = PromptChoice();

            if (choice == "View Diff")
            {
                ShowDiff(stagingFile, destFile);
                continue;
            }

            if (choice == "Overwrite")
            {
                if (!AnsiConsole.Confirm($"Are you sure you want to overwrite {EscapeMarkup(destFile)}?"))
                {
                    continue;
                }

                return ConflictResolution.Overwrite;
            }

            if (choice == "Abort Restore")
            {
                AnsiConsole.MarkupLine("[red][[ConflictResolver]][/] Restore aborted by user.");
                return ConflictResolution.Abort;
            }

            return ConflictResolution.Skip;
        }
    }

    private static string PromptChoice()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("How would you like to resolve this [yellow]conflict[/]?")
                .AddChoices("Overwrite", "Skip", "View Diff", "Abort Restore")
        );
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static void ShowDiff(string stagingFile, string destFile)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Diff[/]: {EscapeMarkup(stagingFile)} vs {EscapeMarkup(destFile)}");
        AnsiConsole.MarkupLine("[grey]--- system file[/]");
        AnsiConsole.MarkupLine("[grey]+++ backup file[/]");

        try
        {
            var stagingInfo = new FileInfo(stagingFile);
            var destInfo = new FileInfo(destFile);

            if (stagingInfo.Length > MaxDiffFileSize || destInfo.Length > MaxDiffFileSize)
            {
                AnsiConsole.MarkupLine("[yellow][[ConflictResolver]][/] File too large for inline diff. Showing first 50 lines per file.");
                ShowPartialDiff(stagingFile, destFile, maxLines: 50);
                AnsiConsole.WriteLine();
                return;
            }

            string[] stagingLines = File.ReadAllLines(stagingFile);
            string[] destLines = File.ReadAllLines(destFile);

            ShowLcsDiff(destLines, stagingLines);
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red][[ConflictResolver]][/] Cannot read file for diff: {EscapeMarkup(ex.Message)}");
        }
        catch (UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine("[red][[ConflictResolver]][/] Unauthorized access reading files for diff.");
        }

        AnsiConsole.WriteLine();
    }

    private static void ShowPartialDiff(string stagingFile, string destFile, int maxLines)
    {
        string[] stagingLines = ReadFirstLines(stagingFile, maxLines);
        string[] destLines = ReadFirstLines(destFile, maxLines);

        int max = Math.Max(stagingLines.Length, destLines.Length);

        for (int i = 0; i < max; i++)
        {
            string? stagingLine = i < stagingLines.Length ? stagingLines[i] : null;
            string? destLine = i < destLines.Length ? destLines[i] : null;

            if (stagingLine == destLine)
            {
                AnsiConsole.MarkupLine($"  {EscapeMarkup(stagingLine!)}");
            }
            else
            {
                if (destLine is not null)
                    AnsiConsole.MarkupLine($"[red]- {EscapeMarkup(destLine)}[/]");
                if (stagingLine is not null)
                    AnsiConsole.MarkupLine($"[green]+ {EscapeMarkup(stagingLine)}[/]");
            }
        }

        if (stagingLines.Length == maxLines || destLines.Length == maxLines)
        {
            AnsiConsole.MarkupLine("[grey]... (truncated)[/]");
        }
    }

    private static string[] ReadFirstLines(string filePath, int maxLines)
    {
        var lines = new List<string>(maxLines);

        using var reader = new StreamReader(filePath);
        for (int i = 0; i < maxLines; i++)
        {
            string? line = reader.ReadLine();
            if (line is null)
                break;
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static void ShowLcsDiff(string[] destLines, string[] stagingLines)
    {
        int[,] lcs = ComputeLcsTable(destLines, stagingLines);
        var operations = new List<(char op, string line)>();

        BacktrackLcs(operations, destLines, stagingLines, lcs, destLines.Length, stagingLines.Length);

        operations.Reverse();

        int contextLines = 3;
        int consecutiveContext = 0;
        bool inChangeBlock = false;

        for (int i = 0; i < operations.Count; i++)
        {
            var (op, line) = operations[i];

            if (op == ' ')
            {
                consecutiveContext++;
            }
            else
            {
                consecutiveContext = 0;
                inChangeBlock = true;
            }

            if (consecutiveContext > contextLines && i + 1 < operations.Count)
            {
                bool upcomingChange = false;
                for (int j = i + 1; j < operations.Count && j <= i + contextLines; j++)
                {
                    if (operations[j].op != ' ')
                    {
                        upcomingChange = true;
                        break;
                    }
                }

                if (!upcomingChange && inChangeBlock)
                {
                    AnsiConsole.MarkupLine("[grey]...[/]");
                    inChangeBlock = false;
                    continue;
                }
            }

            switch (op)
            {
                case ' ':
                    AnsiConsole.MarkupLine($"  {EscapeMarkup(line)}");
                    break;
                case '-':
                    AnsiConsole.MarkupLine($"[red]- {EscapeMarkup(line)}[/]");
                    break;
                case '+':
                    AnsiConsole.MarkupLine($"[green]+ {EscapeMarkup(line)}[/]");
                    break;
            }
        }
    }

    private static int[,] ComputeLcsTable(string[] a, string[] b)
    {
        int m = a.Length;
        int n = b.Length;
        var lcs = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                }
                else
                {
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                }
            }
        }

        return lcs;
    }

    private static void BacktrackLcs(List<(char op, string line)> operations, string[] a, string[] b, int[,] lcs, int i, int j)
    {
        if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
        {
            BacktrackLcs(operations, a, b, lcs, i - 1, j - 1);
            operations.Add((' ', a[i - 1]));
            return;
        }

        if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
        {
            BacktrackLcs(operations, a, b, lcs, i, j - 1);
            operations.Add(('+', b[j - 1]));
            return;
        }

        if (i > 0 && (j == 0 || lcs[i, j - 1] < lcs[i - 1, j]))
        {
            BacktrackLcs(operations, a, b, lcs, i - 1, j);
            operations.Add(('-', a[i - 1]));
        }
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}