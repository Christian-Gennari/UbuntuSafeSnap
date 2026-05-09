using System.Diagnostics;

namespace UbuntuSafeSnap.Services;

public class PackageService : Interfaces.IPackageService
{
    public async Task ExtractPackageListAsync(string stagingDirectory)
    {
        if (string.IsNullOrWhiteSpace(stagingDirectory))
        {
            throw new ArgumentException("staging directory can not be null");
        }

        // if the directory does not exist then create it
        if (!Directory.Exists(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
        }

        Console.WriteLine("[PackageService] Reading manually installed packages...");

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

        Console.WriteLine($"[PackageService] Found {packageCount} manually installed package(s).");
    }
}
