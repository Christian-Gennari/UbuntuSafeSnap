// See https://aka.ms/new-console-template for more information
using UbuntuSafeSnap;
using UbuntuSafeSnap.Services;

const string targetsFile = "targets.txt";

if (!File.Exists(targetsFile))
{
    File.WriteAllText(targetsFile, "# Add directories here, one per line" + Environment.NewLine + "tests/mock_system" + Environment.NewLine);
    Console.WriteLine($"[{targetsFile}] did not exist. A starter file has been created.");
    Console.WriteLine("Please review / edit it, then rerun the program.");
    Console.Write("Press Enter to exit...");
    Console.ReadLine();
    return;
}

var targetDirectories = TargetResolverService.Resolve(targetsFile).ToArray();

string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

var packageService = new PackageService();
await packageService.ExtractPackageListAsync(stagingDirectory);

var configService = new ConfigService();
await configService.CollectConfigFilesAsync(targetDirectories, stagingDirectory);

Console.WriteLine($"Done! Check: {stagingDirectory}");
