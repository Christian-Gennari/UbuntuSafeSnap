using Microsoft.Extensions.DependencyInjection;
using UbuntuSafeSnap.Interfaces;
using UbuntuSafeSnap.Services;

const string targetsFile = "targets.txt";
const string exclusionsFile = "exclusions.txt";

if (!File.Exists(targetsFile))
{
    File.WriteAllText(targetsFile, "# Add directories here, one per line" + Environment.NewLine + "tests/mock_system" + Environment.NewLine);
    Console.WriteLine($"[{targetsFile}] did not exist. A starter file has been created.");
    Console.WriteLine("Please review / edit it, then rerun the program.");
    Console.Write("Press Enter to exit...");
    Console.ReadLine();
    return;
}

if (!File.Exists(exclusionsFile))
{
    File.WriteAllText(exclusionsFile, new ExclusionService(exclusionsFile).GetDefaultContent());
    Console.WriteLine($"[{exclusionsFile}] did not exist. A starter file has been created.");
    Console.WriteLine("Please review / edit it, then rerun the program.");
    Console.Write("Press Enter to exit...");
    Console.ReadLine();
    return;
}

var services = new ServiceCollection()
    .AddSingleton<ITargetResolverService, TargetResolverService>()
    .AddSingleton<IExclusionService, ExclusionService>(provider => new ExclusionService(exclusionsFile))
    .AddSingleton<IPackageService, PackageService>()
    .AddSingleton<IConfigService, ConfigService>()
    .AddSingleton<IArchiveService, ArchiveService>()
    .BuildServiceProvider();

var targetResolver = services.GetRequiredService<ITargetResolverService>();
var targetDirectories = targetResolver.Resolve(targetsFile).ToArray();

string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

var packageService = services.GetRequiredService<IPackageService>();
await packageService.ExtractPackageListAsync(stagingDirectory);

var configService = services.GetRequiredService<IConfigService>();
await configService.CollectConfigFilesAsync(targetDirectories, stagingDirectory);

string archivePath = Path.Combine(
    Directory.GetCurrentDirectory(),
    $"ubuntusafesnap-{DateTime.Now:yyyyMMdd-HHmm}.zip"
);

var archiveService = services.GetRequiredService<IArchiveService>();
archiveService.CreateArchive(stagingDirectory, archivePath);