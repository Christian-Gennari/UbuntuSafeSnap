namespace UbuntuSafeSnap.Interfaces;

public interface IExclusionService
{
    string GetDefaultContent();
    void Load(string filePath);
    bool ShouldExclude(string filePath);
}
