namespace UbuntuSafeSnap.Interfaces;

public interface IConfigService
{
    Task CollectConfigFilesAsync(IEnumerable<string> sourceDirectories, string stagingDirectory);
}
