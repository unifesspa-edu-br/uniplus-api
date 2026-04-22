namespace Unifesspa.UniPlus.Infrastructure.Core.Logging;

using System.Globalization;

using Microsoft.Extensions.Configuration;

using Serilog;
using Serilog.Events;

public static class SerilogConfiguration
{
    public static LoggerConfiguration ConfigurarSerilog(this LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(loggerConfiguration);

        return loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<PiiMaskingEnricher>()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
    }
}
