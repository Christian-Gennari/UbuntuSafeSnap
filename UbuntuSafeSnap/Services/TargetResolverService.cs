namespace UbuntuSafeSnap.Services;

public static class TargetResolverService
{
    public static IEnumerable<string> Resolve(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Target file not found: {filePath}",
                filePath);
        }

        foreach (var raw in File.ReadAllLines(filePath))
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            string expanded = line.StartsWith('~')
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + line[1..]
                : line;

            yield return Path.GetFullPath(expanded);
        }
    }
}
