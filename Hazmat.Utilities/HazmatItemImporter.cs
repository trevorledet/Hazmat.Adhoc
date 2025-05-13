using System.Diagnostics.CodeAnalysis;
using System.Management;
using Hazmat.Common.Models;
using Hazmat.Common.Repositories;
using Hazmat.Utilities.Clients;
using Hazmat.Utilities.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Hazmat.Utilities;

public enum ProcessModes
{
    NotSet,
    Load,
    Identify,
    Update,
    Clear
}

public class PrevData
{
    public required string Symbol { get; set; }
    public required string[] ProperNames { get; set; }
    public required string Description { get; set; }
    public required string Class { get; set; }
    public required string Id { get; set; }
}

public class HazmatItemImporter
{
    private PrevData prevData = new PrevData() { Symbol = string.Empty, ProperNames = [], Description = string.Empty, Class = string.Empty, Id = string.Empty };
    private readonly ReplaceList[] _replacements = new[]
    {
        new ReplaceList { Original = "<TD class=\"right\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"right border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"left border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"center border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"center border-bottom-single border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"left border-bottom-single border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"right border-bottom-single border-right-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"right border-bottom-single\">", Replacement = "<TD>" },
        new ReplaceList { Original = "<TD class=\"left border-right-single\"/>", Replacement = "<TD />" },
        new ReplaceList { Original = "<TD class=\"right\"/>", Replacement = "<TD />" },
        new ReplaceList { Original = "<TD class=\"right border-right-single\"/>", Replacement = "<TD />" },
        new ReplaceList { Original = "<TD class=\"center border-right-single\"/>", Replacement = "<TD />" }
    };

    private const string beginOfRow = "<TR>";
    private const string endOfRow = "</TR>";
    private const string beginOfCell = "<TD>";
    private const string endOfCell = "</TD>";
    private const string emptyCell = "<TD />";

    private const string forbidden = "FBDN";
    private const string see = "SEE";

    private readonly HazmatImporterSettings _hazmatSettings;
    private readonly Title49ApiSettings _title49ApiSettings;

    private readonly IHazmatRepository<HazmatItem> _hazmatRepository;
    private readonly ECFRClient httpHazmatClient;
    private readonly ILogger<HazmatItemImporter> _logger;
    private readonly IConfiguration _configuration;

    public HazmatItemImporter(IHazmatRepository<HazmatItem> hazmatRepository, ECFRClient httpHazmatClient,
                              ILogger<HazmatItemImporter> logger, IConfiguration configuration)
    {
        _hazmatRepository = hazmatRepository;
        this.httpHazmatClient = httpHazmatClient;
        _logger = logger;

        _hazmatSettings = configuration.GetSection("HazmatImporter").Get<HazmatImporterSettings>() ?? throw new ArgumentNullException("HazmatImporter settings not set in configuration");
        _title49ApiSettings = configuration.GetSection("Title49Api").Get<Title49ApiSettings>() ?? throw new ArgumentNullException("Title49Api settings not set in configuration");

        _configuration = configuration;
    }

    public async Task ImportHazmatItemsAsync()
    {
        const string dataFileSuffix = "HazmatItems.htm";

        string rawData = string.Empty;
        switch (_hazmatSettings.Mode)
        {
            case ProcessModes.Load:
            case ProcessModes.Identify:
                // string rawData = File.ReadAllText(sourcePath);
                rawData = await httpHazmatClient.GetHazmatSectionAsync(_hazmatSettings.HazmatSection);

                if (string.IsNullOrEmpty(rawData))
                {
                    _logger.LogError("No data returned from API call");
                    return;
                }
                File.WriteAllText($"{_title49ApiSettings.DataRoot}{_title49ApiSettings.ReportDate}{dataFileSuffix}", rawData);
                break;
            case ProcessModes.Update:
                rawData = File.ReadAllText($"{_title49ApiSettings.DataRoot}{_title49ApiSettings.ReportDate}{dataFileSuffix}");
                if (string.IsNullOrEmpty(rawData))
                {
                    _logger.LogError("No data found in file {File}", $"{_title49ApiSettings.DataRoot}{_title49ApiSettings.ReportDate}{dataFileSuffix}");
                    return;
                }
                break;
            case ProcessModes.Clear:
                _logger.LogWarning("Clearing Hazmat Items");
                await new ClearTables<HazmatItem>(_hazmatRepository, _configuration).Clear();
                _logger.LogWarning("Clearing Hazmat Items complete");
                return;
            default:
                _logger.LogError("Process mode not set");
                return;
        }
        _logger.LogWarning("Mode {mode} Starting", _hazmatSettings.Mode.ToString());


        HazmatItem newHazmatItem = new HazmatItem();

        int count = 0;

        // Position to start of Hazmat Table
        long currentPosition = rawData.IndexOf(_hazmatSettings.StartTag);
        if (currentPosition == -1)
        {
            _logger.LogError("Start tag not found in source file");
            return;
        }
        // Position to 1st row.
        currentPosition = rawData.IndexOf("</THEAD>", (int)currentPosition);
        currentPosition = rawData.IndexOf(endOfRow, (int)currentPosition) + 5;
        long endPosition = rawData.IndexOf("</TBODY>", (int)currentPosition);
        rawData = rawData.Substring((int)currentPosition, (int)(endPosition - currentPosition));
        currentPosition = 0;

        foreach (var replacement in _replacements)
        {
            rawData = rawData.Replace(replacement.Original, replacement.Replacement);
        }


        while (currentPosition < rawData.Length)
        {
            try
            {

                int nextPosition = rawData.IndexOf(endOfRow, (int)currentPosition);
                if (nextPosition == -1)
                {
                    break;
                }

                count++;

                string row = rawData.Substring((int)currentPosition, (int)(nextPosition - currentPosition));
                currentPosition = nextPosition + 5;

                // Because of Previous Data requirements rows need to be proceeded in sequence and not in parallel.
                row = row.Replace("\r\n", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Replace(beginOfRow, string.Empty).Replace(endOfRow, string.Empty).Trim();

                object[] columns = row.Split([endOfCell, emptyCell], StringSplitOptions.None);
                if (_hazmatSettings.Mode != ProcessModes.Update || (_hazmatSettings.Mode == ProcessModes.Update && columns.Length > 15))
                {

                    newHazmatItem = new HazmatItem(columns);

                    if (newHazmatItem.IsSeeSkip() && newHazmatItem.Class.ToString().IsNullOrEmpty() && newHazmatItem.UNId.ToString().IsNullOrEmpty() && newHazmatItem.PackageGroup.ToString().IsNullOrEmpty())
                    {
                        newHazmatItem.Class = see;
                    }

                    if (newHazmatItem.Symbols.ToString().IsNullOrEmpty() && (newHazmatItem.ProperShippingNames.Length == 0 || newHazmatItem.ProperShippingNames[0].ToString().IsNullOrEmpty()) &&
                        newHazmatItem.Class.ToString().IsNullOrEmpty() && newHazmatItem.UNId.ToString().IsNullOrEmpty() &&
                        !newHazmatItem.PackageGroup.ToString().IsNullOrEmpty())
                    {
                        newHazmatItem.Symbols = prevData.Symbol;
                        newHazmatItem.ProperShippingNames = prevData.ProperNames;
                        newHazmatItem.Description = prevData.Description;
                        newHazmatItem.Class = prevData.Class;
                        newHazmatItem.UNId = prevData.Id;
                    }
                    else
                    {
                        prevData = new PrevData
                        {
                            Symbol = newHazmatItem.Symbols,
                            ProperNames = newHazmatItem.ProperShippingNames,
                            Description = newHazmatItem.Description,
                            Class = newHazmatItem.Class,
                            Id = newHazmatItem.UNId
                        };
                    }

                    switch (_hazmatSettings.Mode)
                    {
                        case ProcessModes.Identify:
                            await IdentifyChangedItem(newHazmatItem);
                            break;
                        case ProcessModes.Update:
                            await ProcessUpdatedItem(newHazmatItem, columns);
                            break;
                        case ProcessModes.Load:
                            await _hazmatRepository.AddAsync(newHazmatItem);
                            break;
                        default:
                            _logger.LogError("Process mode not set");
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error DB action updating/adding hazmat item: {HazmatItem}", newHazmatItem);
            }
            if (_hazmatSettings.PauseInterval > 0 && count % _hazmatSettings.PauseInterval == 0)
            {
                Thread.Sleep(_hazmatSettings.PauseDelay);
                _logger.LogWarning("Processing {Count} hazmat item", count);
            }
            _hazmatRepository.ClearTracking();
        }
        _logger.LogWarning("Mode {mode} completed, processed {Count} hazmat items", _hazmatSettings.Mode.ToString(), count);

        return;
    }

    private async Task IdentifyChangedItem(HazmatItem newHazmatItem)
    {
        IEnumerable<HazmatItem> existingItems = new List<HazmatItem>();
        HazmatItem? matchingMiscItem = null;
        if (newHazmatItem.Class == forbidden || newHazmatItem.Class == see)
        {
            matchingMiscItem = await _hazmatRepository.GetByNameAsync(newHazmatItem.ProperShippingNames[0], newHazmatItem.Class);
        }
        else
        {
            existingItems = await _hazmatRepository.GetByIdAsync(newHazmatItem.UNId, newHazmatItem.PackageGroup);
        }
        if (matchingMiscItem == null && existingItems.Count() == 0)
        {
            _logger.LogWarning("New hazmat item not found in database: {HazmatItem}", newHazmatItem.ToString());
        }
        else
        {
            if (matchingMiscItem != null)
            {
                existingItems = existingItems.Append<HazmatItem>(matchingMiscItem);
            }

            if (existingItems.Count() == 0)
            {
                _logger.LogWarning("Existing or simular hazmat items not found in database: {HazmatItem}", newHazmatItem.ToString());
            }
            else
            {
                int flag = 0;
                existingItems.ToList().ForEach(h =>
                {
                    newHazmatItem.Id = h.Id;
                    List<string> diffs = ObjectComparer.CompareProperties(newHazmatItem, h);
                    if (diffs.Count == 0)
                    {
                        flag = -1;
                    }
                    else if (flag != -1)
                    {
                        flag = diffs.Count;
                    }
                });
                if (flag != -1)
                {
                    _logger.LogWarning("Changed or new hazmat item found in import: {HazmatItem}", newHazmatItem.ToString());
                }
            }
        }
    }

    private async Task ProcessUpdatedItem(HazmatItem newHazmatItem, object[] columns)
    {
        const string cU = "U";
        if (columns.Length < 15 || (columns[14].ToString() == cU && columns.Length < 17))
        {
            _logger.LogWarning("Row has {ColumnCount} columns: {Row}", columns.Length, string.Join(",", columns));
            return;
        }

        switch (columns[14])
        {
            case "A":
                await _hazmatRepository.AddAsync(newHazmatItem);
                _logger.LogWarning("Added hazmat item: {ID}, {UNId}, {PG}", newHazmatItem.Id, newHazmatItem.UNId, newHazmatItem.PackageGroup);
                break;

            case cU:
                try
                {
                    if (!string.IsNullOrEmpty(columns[15]?.ToString()))
                    {
#pragma warning disable CS8604 // Possible null reference argument.
                        newHazmatItem.Id = Guid.Parse(columns[15].ToString());
#pragma warning restore CS8604 // Possible null reference argument.
                    }
                    else
                    {
                        _logger.LogWarning("Empty GUID on row {row}.", string.Join(",", columns));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing GUID: {Guid} on row {row}.", columns[16], string.Join(",", columns));
                    return;
                }
                await _hazmatRepository.UpdateAsync(newHazmatItem);
                _logger.LogWarning("Updated hazmat item: {ID}, {UNId}, {PG}", newHazmatItem.Id, newHazmatItem.UNId, newHazmatItem.PackageGroup);
                break;

            default:
                _logger.LogWarning("Unknown action: {Action} on row {Row}", columns[15], string.Join(",", columns));
                break;
        }
    }
}