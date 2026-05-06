namespace UbuntuSafeSnap.Interfaces;

public interface ITargetResolverService
{
    IEnumerable<string> Resolve(string filePath);
}
