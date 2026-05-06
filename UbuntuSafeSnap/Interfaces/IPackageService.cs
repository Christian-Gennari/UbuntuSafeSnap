namespace UbuntuSafeSnap.Interfaces;

public interface IPackageService
{
    Task ExtractPackageListAsync(string stagingDirectory);
}
