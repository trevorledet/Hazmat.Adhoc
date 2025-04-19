using Hazmat.Utilities;
using Hazmat.Common.Data;
using TestService;
using Hazmat.Common.Models;
using Hazmat.Utilities.Clients;
using Hazmat.Utilities.Models;
using TestService.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Custom extension method in TestService.Extensions.LoggingService to  setup logging output and levels.
builder.Services.ConfigureCustomLogging(builder.Configuration);

// Add base Http Client for pulling Hazmat Meta Data.
builder.Services.AddHttpHazmatClients(builder.Configuration);

// Register the database connection to Azure CosmosDB or Google FireStore NoSql determined by config
// and HazmatItemImporter as a scoped service for use in Worker.
builder.Services.AddDatabaseConnection(builder.Configuration);
builder.Services.AddScoped<HazmatItemImporter>();

//Added Utility class for retrievaling Hazmat sections meta data.
builder.Services.AddScoped<ECFRClient>();
// Register Worker as a scoped service
builder.Services.AddScoped<Worker>();

// Manually add Worker to the hosted services
builder.Services.AddSingleton<IHostedService>(provider =>
{
    var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    var scope = scopeFactory.CreateScope();
    return scope.ServiceProvider.GetRequiredService<Worker>();
});

var host = builder.Build();
host.Run();
