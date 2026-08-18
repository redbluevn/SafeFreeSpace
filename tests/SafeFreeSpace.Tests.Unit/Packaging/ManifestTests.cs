namespace SafeFreeSpace.Tests.Unit.Packaging;

using System.Reflection;
using System.Xml.Linq;
using Xunit;

public class ManifestTests
{
    [Fact]
    public void AppManifest_IsAsInvoker()
    {
        string? path = FindManifest("SafeFreeSpace.App");
        Assert.NotNull(path);
        XDocument doc = XDocument.Load(path);
        XElement? level = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "requestedExecutionLevel");
        Assert.NotNull(level);
        Assert.Equal("asInvoker", level.Attribute("level")?.Value);
    }

    [Fact]
    public void WorkerManifest_IsRequireAdministrator()
    {
        string? path = FindManifest("SafeFreeSpace.ElevatedWorker");
        Assert.NotNull(path);
        XDocument doc = XDocument.Load(path);
        XElement? level = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "requestedExecutionLevel");
        Assert.NotNull(level);
        Assert.Equal("requireAdministrator", level.Attribute("level")?.Value);
    }

    private static string? FindManifest(string projectName)
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string? current = Path.GetDirectoryName(assemblyLocation);
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "src", projectName, "app.manifest");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Path.GetDirectoryName(current);
            if (parent == current)
            {
                break;
            }

            current = parent;
        }

        return null;
    }
}
