namespace Anduril.Setup.Tests;

public class FindConfigPathTests
{
    // Creates a fresh temp root for each test and cleans it up on exit
    private static string MakeTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task FindConfigPath_ExplicitPathExists_ReturnsThatPath()
    {
        var root = MakeTempRoot();
        try
        {
            var configFile = Path.Combine(root, "appsettings.json");
            File.WriteAllText(configFile, "{}");

            var result = SetupService.FindConfigPath(configFile, root);

            await Assert.That(result).IsEqualTo(configFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_ExplicitPathDoesNotExist_FallsThroughToSearch()
    {
        var root = MakeTempRoot();
        try
        {
            // Create src/Anduril.Host/appsettings.json so the fallback succeeds
            var hostDir = Path.Combine(root, "src", "Anduril.Host");
            Directory.CreateDirectory(hostDir);
            var configFile = Path.Combine(hostDir, "appsettings.json");
            File.WriteAllText(configFile, "{}");

            var result = SetupService.FindConfigPath("/nonexistent/path/appsettings.json", root);

            await Assert.That(result).IsEqualTo(configFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_NullExplicitPath_FindsSrcAndurilHostLayout()
    {
        var root = MakeTempRoot();
        try
        {
            var hostDir = Path.Combine(root, "src", "Anduril.Host");
            Directory.CreateDirectory(hostDir);
            var configFile = Path.Combine(hostDir, "appsettings.json");
            File.WriteAllText(configFile, "{}");

            // Start search from a subdirectory so the walk-up is exercised
            var subDir = Path.Combine(root, "tests", "some-test");
            Directory.CreateDirectory(subDir);

            var result = SetupService.FindConfigPath(null, subDir);

            await Assert.That(result).IsEqualTo(configFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_NullExplicitPath_FindsSiblingAndurilHostLayout()
    {
        var root = MakeTempRoot();
        try
        {
            // Layout: root/Anduril.Host/appsettings.json (no src/ prefix)
            var hostDir = Path.Combine(root, "Anduril.Host");
            Directory.CreateDirectory(hostDir);
            var configFile = Path.Combine(hostDir, "appsettings.json");
            File.WriteAllText(configFile, "{}");

            var result = SetupService.FindConfigPath(null, root);

            await Assert.That(result).IsEqualTo(configFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_NullExplicitPath_SrcLayoutTakesPriorityOverSiblingLayout()
    {
        var root = MakeTempRoot();
        try
        {
            // Both layouts exist at the same directory level
            var srcHostDir = Path.Combine(root, "src", "Anduril.Host");
            Directory.CreateDirectory(srcHostDir);
            var srcConfigFile = Path.Combine(srcHostDir, "appsettings.json");
            File.WriteAllText(srcConfigFile, "{}");

            var siblingHostDir = Path.Combine(root, "Anduril.Host");
            Directory.CreateDirectory(siblingHostDir);
            File.WriteAllText(Path.Combine(siblingHostDir, "appsettings.json"), "{}");

            var result = SetupService.FindConfigPath(null, root);

            await Assert.That(result).IsEqualTo(srcConfigFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_NullExplicitPath_FallsBackToAnyAppSettingsJson()
    {
        var root = MakeTempRoot();
        try
        {
            // No Anduril.Host directory — just a plain appsettings.json at root
            var configFile = Path.Combine(root, "appsettings.json");
            File.WriteAllText(configFile, "{}");

            var result = SetupService.FindConfigPath(null, root);

            await Assert.That(result).IsEqualTo(configFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindConfigPath_NullExplicitPath_ReturnsNullWhenNothingFound()
    {
        var root = MakeTempRoot();
        try
        {
            // Completely empty temp directory — no config files anywhere inside it
            var deepDir = Path.Combine(root, "a", "b", "c");
            Directory.CreateDirectory(deepDir);

            // Pass the deepest dir as both start and cap the walk at root by starting from
            // a path that won't escape into real system directories via the walk-up.
            // We search from the deep dir but the walk-up is unrestricted, so we only
            // rely on the temp dir being clean; there should be no appsettings.json above.
            var result = SetupService.FindConfigPath(null, deepDir);

            // The temp directory tree has no appsettings.json; the result should be null
            // (assuming no appsettings.json exists in ancestor dirs up to the filesystem root,
            // which is the normal case on a developer machine).
            await Assert.That(result).IsNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

