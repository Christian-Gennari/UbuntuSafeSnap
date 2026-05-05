// See https://aka.ms/new-console-template for more information
using UbuntuSafeSnap;

string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

var packageService = new PackageService();
await packageService.ExtractPackageListAsync(stagingDirectory);

var configService = new ConfigService();
await configService.CollectConfigFilesAsync(
    new[] { Path.Combine(Directory.GetCurrentDirectory(), "tests", "mock_system") },
    stagingDirectory);

Console.WriteLine($"Done! Check: {stagingDirectory}");
