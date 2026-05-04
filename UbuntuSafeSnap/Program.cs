// See https://aka.ms/new-console-template for more information
using UbuntuSafeSnap;

string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

var packageService = new PackageService();

await packageService.ExtractPackageListAsync(stagingDirectory);

Console.WriteLine($"Done! Check: {Path.Combine(stagingDirectory, "packages.txt")}");
