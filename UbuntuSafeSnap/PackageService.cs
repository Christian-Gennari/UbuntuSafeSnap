using System.Diagnostics;

namespace UbuntuSafeSnap;

public class PackageService
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

        Console.WriteLine($"[PackageService] Starting package extraction to: {stagingDirectory}");

        using var process = new Process();
        // Configure process to run apt-mark showmanual
        process.StartInfo.FileName = "apt-mark";
        process.StartInfo.Arguments = "showmanual";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine($"[PackageService] apt-mark exited with code: {process.ExitCode}");

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"apt-mark call failed with exit code {process.ExitCode}"
            );

        string outputPath = Path.Combine(stagingDirectory, "packages.txt");

        Console.WriteLine(
            $"[PackageService] Read {output.Length} characters. Writing to {outputPath}..."
        );

        await File.WriteAllTextAsync(outputPath, output);

        Console.WriteLine("[PackageService] Done.");
    }
}
