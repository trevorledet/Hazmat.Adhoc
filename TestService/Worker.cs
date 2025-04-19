using Hazmat.Common.Models;
using Hazmat.Utilities;
using Newtonsoft.Json;

namespace TestService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HazmatItemImporter _importer;

    public Worker(ILogger<Worker> logger, HazmatItemImporter importer)
    {
        _logger = logger;
        _importer = importer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);
            }
            await _importer.ImportHazmatItems();
            break;
        }
    }
}
