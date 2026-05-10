using System.Security.Cryptography;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Spectre.Console;
using UbuntuSafeSnap.Models;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Restore;

public class ConflictResolverService
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
            AnsiConsole.MarkupLine($"[green][[ConflictResolver]][/] Files are identical, skipping: {ConsolePrompt.EscapeMarkup(destFile)}");
            return ConflictResolution.Identical;
        }

        if (!ConsolePrompt.IsInteractive)
        {
            Console.Error.WriteLine($"[ConflictResolver] Conflict requires user input: {destFile}");
            Console.Error.WriteLine("[ConflictResolver] Re-run in an interactive terminal to resolve.");
            return ConflictResolution.Abort;
        }

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow][[ConflictResolver]][/] Conflict detected:");
            AnsiConsole.MarkupLine($"  [grey]Backup: [/] {ConsolePrompt.EscapeMarkup(stagingFile)}");
            AnsiConsole.MarkupLine($"  [grey]System:[/] {ConsolePrompt.EscapeMarkup(destFile)}");

            var choice = ConsolePrompt.PromptSelection(
                "How would you like to resolve this conflict?",
                ["Overwrite", "Skip", "View Diff", "Abort Restore"]
            );

            if (choice == "View Diff")
            {
                ShowDiff(stagingFile, destFile);
                continue;
            }

            if (choice == "Overwrite")
            {
                if (!ConsolePrompt.Confirm($"Are you sure you want to overwrite {ConsolePrompt.EscapeMarkup(destFile)}?"))
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

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static void ShowDiff(string stagingFile, string destFile)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Diff[/]: {ConsolePrompt.EscapeMarkup(stagingFile)} vs {ConsolePrompt.EscapeMarkup(destFile)}");
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

            string destText = File.ReadAllText(destFile);
            string stagingText = File.ReadAllText(stagingFile);

            var diff = InlineDiffBuilder.Diff(destText, stagingText, ignoreWhiteSpace: false);

            foreach (var line in diff.Lines)
            {
                if (line.Type == ChangeType.Imaginary)
                    continue;

                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        AnsiConsole.MarkupLine($"[green]+ {ConsolePrompt.EscapeMarkup(line.Text)}[/]");
                        break;
                    case ChangeType.Deleted:
                        AnsiConsole.MarkupLine($"[red]- {ConsolePrompt.EscapeMarkup(line.Text)}[/]");
                        break;
                    case ChangeType.Modified:
                        AnsiConsole.MarkupLine($"[yellow]~ {ConsolePrompt.EscapeMarkup(line.Text)}[/]");
                        break;
                    default:
                        AnsiConsole.MarkupLine($"  {ConsolePrompt.EscapeMarkup(line.Text)}");
                        break;
                }
            }
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red][[ConflictResolver]][/] Cannot read file for diff: {ConsolePrompt.EscapeMarkup(ex.Message)}");
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
                AnsiConsole.MarkupLine($"  {ConsolePrompt.EscapeMarkup(stagingLine!)}");
            }
            else
            {
                if (destLine is not null)
                    AnsiConsole.MarkupLine($"[red]- {ConsolePrompt.EscapeMarkup(destLine)}[/]");
                if (stagingLine is not null)
                    AnsiConsole.MarkupLine($"[green]+ {ConsolePrompt.EscapeMarkup(stagingLine)}[/]");
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
}