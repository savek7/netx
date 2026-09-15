using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Tools.Logging;

public static class Installer
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddLogging(ILoggingBuilder builder, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            builder.ClearProviders();
            serviceCollection.AddSerilog(configureLogger =>
            {
                configureLogger.MinimumLevel.Is(GetMinimumLevel(configuration));
                configureLogger.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning);

                configureLogger
                    .WriteTo.Console()
                    .WriteTo.Conditional(le => bool.Parse(configuration["SerilogLogging:File:Enabled"]!) && !string.IsNullOrEmpty(configuration["SerilogLogging:File:Path"]),
                        configureSink => configureSink.Async(t => t.File(configuration["SerilogLogging:File:Path"]!, rollingInterval: RollingInterval.Day)));
            });

            return serviceCollection;
        }
    }

    static LogEventLevel GetMinimumLevel(IConfiguration configuration) => configuration["Logging:Serilog:LogLevel:Default"] switch
    {
        "Trace" => LogEventLevel.Verbose,
        "Debug" => LogEventLevel.Debug,
        "Information" => LogEventLevel.Information,
        "Warning" => LogEventLevel.Warning,
        "Error" => LogEventLevel.Error,
        "Critical" => LogEventLevel.Fatal,

        _ => LogEventLevel.Information
    };
}

