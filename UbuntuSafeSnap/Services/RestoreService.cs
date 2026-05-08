using System.Diagnostics;
using System.IO.Compression;
using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class RestoreService : IRestoreService
{
    public async Task<int> RestoreAsync(string archivePath)
    {
        ArgumentNullException.ThrowIfNull(archivePath);

        if (Environment.UserName != "root")
        {
            Console.Error.WriteLine("Run this command with sudo.");
            return 1;
        }

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"[RestoreService] Archive not found: {archivePath}");
            return 1;
        }

        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ubuntusafesnap-restore-{Guid.NewGuid():N}"
        );

        try
        {
            Console.WriteLine($"[RestoreService] Extracting archive to: {stagingDirectory}");

            try
            {
                ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"[RestoreService] Archive is corrupted or invalid: {ex.Message}");
                return 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"[RestoreService] IO error extracting archive: {ex.Message}");
                return 1;
            }

            Console.WriteLine("[RestoreService] Archive extracted successfully.");

            int packageResult = await ReinstallPackagesAsync(stagingDirectory);
            if (packageResult != 0)
            {
                Console.Error.WriteLine("[RestoreService] Package re-installation failed. Aborting restore.");
                return packageResult;
            }

            var (restored, skipped) = RestoreConfigFiles(stagingDirectory);

            Console.WriteLine($"[RestoreService] Restore complete. {restored} file(s) restored. {skipped} file(s) skipped (already exist).");

            if (skipped > 0)
            {
                Console.WriteLine("[RestoreService] Skipped files need conflict resolution (see #26).");
            }

            return 0;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
                Console.WriteLine($"[RestoreService] Cleaned up staging directory: {stagingDirectory}");
            }
        }
    }

    private static async Task<int> ReinstallPackagesAsync(string stagingDirectory)
    {
        string packagesFile = Path.Combine(stagingDirectory, "packages.txt");

        if (!File.Exists(packagesFile))
        {
            Console.WriteLine("[RestoreService] No packages.txt found in archive. Skipping package re-installation.");
            return 0;
        }

        string[] packages = await File.ReadAllLinesAsync(packagesFile);

        packages = packages
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (packages.Length == 0)
        {
            Console.WriteLine("[RestoreService] packages.txt is empty. No packages to reinstall.");
            return 0;
        }

        Console.WriteLine($"[RestoreService] Re-installing {packages.Length} package(s)...");

        using var process = new Process();
        process.StartInfo.FileName = "sudo";
        process.StartInfo.ArgumentList.Add("apt");
        process.StartInfo.ArgumentList.Add("install");
        process.StartInfo.ArgumentList.Add("-y");

        foreach (var pkg in packages)
        {
            process.StartInfo.ArgumentList.Add(pkg);
        }

        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;

        process.Start();

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"[RestoreService] apt install failed with exit code {process.ExitCode}: {stderr}");
            return process.ExitCode;
        }

        Console.WriteLine("[RestoreService] Package re-installation complete.");
        return 0;
    }

    private static (int restored, int skipped) RestoreConfigFiles(string stagingDirectory)
    {
        int restored = 0;
        int skipped = 0;

        var manifest = LoadManifest(stagingDirectory);

        var directories = new Queue<string>();
        directories.Enqueue(stagingDirectory);

        while (directories.Count > 0)
        {
            var currentDir = directories.Dequeue();
            string[] files;
            string[] subDirs;

            try
            {
                files = Directory.GetFiles(currentDir);
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[RestoreService] Unauthorized access to {currentDir}. Skipping...");
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine($"[RestoreService] Directory not found: {currentDir}. Skipping...");
                continue;
            }

            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(stagingDirectory, file);

                if (relativePath == "packages.txt" || relativePath == "manifest.txt")
                    continue;

                string destPath;

                if (manifest.TryGetValue(relativePath, out string? sourceDir))
                {
                    destPath = Path.Combine(sourceDir, relativePath);
                }
                else
                {
                    destPath = Path.Combine("/", relativePath);
                }

                if (File.Exists(destPath))
                {
                    Console.WriteLine($"[RestoreService] Skipped (already exists): {destPath}");
                    skipped++;
                    continue;
                }

                try
                {
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir is not null && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destPath, overwrite: false);
                    Console.WriteLine($"[RestoreService] Restored: {destPath}");
                    restored++;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"[RestoreService] Unauthorized access to destination: {destPath}");
                    skipped++;
                }
            }

            foreach (var subDir in subDirs)
            {
                directories.Enqueue(subDir);
            }
        }

        return (restored, skipped);
    }

    private static Dictionary<string, string> LoadManifest(string stagingDirectory)
    {
        var manifest = new Dictionary<string, string>();

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");

        if (!File.Exists(manifestPath))
        {
            Console.WriteLine("[RestoreService] No manifest.txt found in archive. Files will be restored to /.");
            return manifest;
        }

        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            int separatorIndex = line.IndexOf('|');
            if (separatorIndex < 0)
                continue;

            string sourceDir = line[..separatorIndex];
            string relativePath = line[(separatorIndex + 1)..];

            manifest[relativePath] = sourceDir;
        }

        Console.WriteLine($"[RestoreService] Loaded manifest with {manifest.Count} entries.");
        return manifest;
    }
}