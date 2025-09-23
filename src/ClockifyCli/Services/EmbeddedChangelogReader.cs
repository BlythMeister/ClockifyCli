using System.Reflection;

namespace ClockifyCli.Services;

/// <summary>
/// Reads changelog from embedded resources
/// </summary>
public class EmbeddedChangelogReader : IChangelogReader
{
    public async Task<string?> ReadChangelogAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ClockifyCli.CHANGELOG.md";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }
            
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return null;
        }
    }
}