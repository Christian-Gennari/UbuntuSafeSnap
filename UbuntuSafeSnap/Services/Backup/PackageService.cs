using System.Diagnostics;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Backup;

public class PackageService
{
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