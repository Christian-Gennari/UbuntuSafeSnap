using Microsoft.Extensions.DependencyInjection;
using UbuntuSafeSnap.Services.Restore;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Commands;

public static class RestoreCommand
{
    public static async Task<int> ExecuteAsync(string? restoreFilePath, bool dryRun = false, bool autoYes = false)
    {
        if (string.IsNullOrWhiteSpace(restoreFilePath))
        {
            string backupsPath = Path.Combine(Directory.GetCurrentDirectory(), "backups");

            if (!Directory.Exists(backupsPath))
            {
                Log.Error("RestoreCommand", $"No backups directory found at {backupsPath}.");
                Log.Error("RestoreCommand", "Run 'ubuntusafesnap backup' first to create a backup.");
                return 1;
            }

            string[] zipFiles = Directory.GetFiles(backupsPath, "*.zip")
                .OrderDescending()
                .ToArray();

            if (zipFiles.Length == 0)
            {
                Log.Error("RestoreCommand", $"No backup files found in {backupsPath}.");
                Log.Error("RestoreCommand", "Run 'ubuntusafesnap backup' first to create a backup.");
                return 1;
            }

            if (zipFiles.Length == 1)
            {
                restoreFilePath = zipFiles[0];
                Log.Info("RestoreCommand", $"Using only available backup: {Path.GetFileName(restoreFilePath)}");
            }
            else
            {
                restoreFilePath = ConsolePrompt.PromptSelection(
                    "Select a backup to restore:",
                    zipFiles
                );
            }
        }

        var services = new ServiceCollection()
            .AddSingleton<ConflictResolverService>()
            .AddSingleton<RestoreService>()
            .BuildServiceProvider();

        var restoreService = services.GetRequiredService<RestoreService>();
        return await restoreService.RestoreAsync(restoreFilePath, dryRun, autoYes);
    }
}
