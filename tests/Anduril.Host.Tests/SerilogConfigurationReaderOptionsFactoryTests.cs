using Microsoft.Extensions.Configuration;
using Serilog;

namespace Anduril.Host.Tests;

public class SerilogConfigurationReaderOptionsFactoryTests
{
    [Test]
    public async Task Create_AllowsConsoleSinkConfigurationToLoad()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:0:Name"] = "Console"
            })
            .Build();

        using var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(
                configuration,
                SerilogConfigurationReaderOptionsFactory.Create())
            .CreateLogger();

        await Assert.That(logger).IsNotNull();
    }
}