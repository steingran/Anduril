using Serilog;
using Serilog.Settings.Configuration;

namespace Anduril.Host;

internal static class SerilogConfigurationReaderOptionsFactory
{
    public static ConfigurationReaderOptions Create()
    {
        return new ConfigurationReaderOptions(typeof(ConsoleLoggerConfigurationExtensions).Assembly);
    }
}