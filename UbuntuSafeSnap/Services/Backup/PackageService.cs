using System.Diagnostics;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Backup;

/// <summary>
/// Captures the list of manually installed packages via apt-mark showmanual
/// and writes them to packages.txt for later reinstallation during restore.
/// </summary>
public class PackageService
{
    /// <summary>
    /// Runs apt-mark showmanual and writes the output to packages.txt in the staging directory.
    /// These packages can be reinstalled during restore via apt install.
    /// </summary>
    /// <param name="stagingDirectory">Directory to write packages.txt into.</param>
    /// <exception cref="InvalidOperationException">Thrown when apt-mark fails.</exception>
    public async Task ExtractPackageListAsync(string stagingDirectory)
    {
        ArgumentNullException.ThrowIfNull(stagingDirectory);

        if (string.IsNullOrWhiteSpace(stagingDirectory))
        {
            throw new ArgumentException("Staging directory cannot be null or empty.", nameof(stagingDirectory));
        }

        if (!Directory.Exists(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
        }

        Log.Info("PackageService", "Reading manually installed packages...");

        using var process = new Process();
        process.StartInfo.FileName = "apt-mark";
        process.StartInfo.Arguments = "showmanual";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"apt-mark call failed with exit code {process.ExitCode}"
            );

        int packageCount = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        string outputPath = Path.Combine(stagingDirectory, "packages.txt");

        await File.WriteAllTextAsync(outputPath, output);

        Log.Info("PackageService", $"Found {packageCount} manually installed package(s).");
    }
}
