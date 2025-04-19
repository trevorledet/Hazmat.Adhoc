using System;
using System.CodeDom;
using Microsoft.AspNetCore.Builder;

namespace TestService.Extensions;

public static class HostBuilderExtensions
{
    public static IServiceCollection ConfigureCustomLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging using the provided configuration
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.SetMinimumLevel(configuration.GetValue<LogLevel>("Logging:LogLevel:Default"));
            loggingBuilder.AddConsole();
            loggingBuilder.AddFile("log.txt");
        });

        return services;
    }

}
