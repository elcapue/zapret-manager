using System.Xml.Linq;

namespace ZapretManager.Tests;

public sealed class AdminManifestTests
{
    [Fact]
    public void AppManifest_StartsAsInvoker_ForSelfElevation()
    {
        // Манифест asInvoker: права администратора запрашиваются через
        // ElevationService после проверки single-instance, чтобы повторный
        // запуск показывал окно без повторного UAC.
        var projectRoot = FindProjectRoot();
        var manifestPath = Path.Combine(projectRoot, "src", "ZapretManager.App", "app.manifest");
        var csprojPath = Path.Combine(projectRoot, "src", "ZapretManager.App", "ZapretManager.App.csproj");

        Assert.True(File.Exists(manifestPath), "app.manifest должен существовать рядом с csproj.");

        var manifest = XDocument.Load(manifestPath);
        var requestedExecutionLevel = manifest
            .Descendants()
            .Single(element => element.Name.LocalName == "requestedExecutionLevel");

        Assert.Equal("asInvoker", requestedExecutionLevel.Attribute("level")?.Value);
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", File.ReadAllText(csprojPath));
    }

    private static string FindProjectRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "ZapretManager.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Не найден корень проекта ZapretManager.sln.");
    }
}
