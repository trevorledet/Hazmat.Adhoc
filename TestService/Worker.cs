using Hazmat.Common.Models;
using Hazmat.Utilities;
using Newtonsoft.Json;

namespace TestService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HazmatItemImporter _importer;
    private readonly SpecialProvisionImporter _specialProvisionImporter;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, 
                  HazmatItemImporter importer,
                  SpecialProvisionImporter specialProvisionImporter,
                  IConfiguration configuration)
    {
        _logger = logger;
        _importer = importer;
        this._specialProvisionImporter = specialProvisionImporter;
        this._configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);
            }
            switch (_configuration["ImportItem"])
            {
                case "SpecialProvisions":
                    await _specialProvisionImporter.ImportSpecialProvisionsAsync();
                    break;
                case "HazmatItems":
                    await _importer.ImportHazmatItemsAsync();
                    break;
                default:
                    _logger.LogWarning("No import item specified. Defaulting to HazmatItems.");
                    break;
            }
            return;
        }
        return;
    }
}
