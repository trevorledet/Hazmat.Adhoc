using System;
using Hazmat.Common.Models;
using Hazmat.Common.Repositories;
using Hazmat.Utilities.Clients;
using Hazmat.Utilities.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using System.Reflection.PortableExecutable;

namespace Hazmat.Utilities;

public class SpecialProvisionImporter
{
    private readonly ICatalogRepository<SpecialProvision> dbRepository;
    private readonly ECFRClient httpHazmatClient;
    private readonly ILogger<SpecialProvisionImporter> logger;
    private readonly IConfiguration configuration;

    private readonly SpecialProvisionSettings spSettings;
    private readonly Title49ApiSettings title49ApiSettings;

    private readonly List<string> DataSections = new List<string>();

    public SpecialProvisionImporter(ICatalogRepository<SpecialProvision> dbRepository, 
                                    ECFRClient eCFRClient,
                                    ILogger<SpecialProvisionImporter> logger,
                                    IConfiguration configuration)
    {
        this.dbRepository = dbRepository;
        this.httpHazmatClient = eCFRClient;
        this.logger = logger;
        this.configuration = configuration;

        spSettings = configuration.GetSection("SpecialProvisionsImporter").Get<SpecialProvisionSettings>() ?? throw new ArgumentNullException("HazmatImporter settings not set in configuration");
        title49ApiSettings = configuration.GetSection("Title49Api").Get<Title49ApiSettings>() ?? throw new ArgumentNullException("Title49Api settings not set in configuration");
    }

    public async Task ImportSpecialProvisionsAsync()
    {
        const string dataFileSuffix = "SpecialProvisions.htm";
        string rawData = string.Empty;

        if (spSettings.Mode != ProcessModes.Clear)
        {
            rawData = await httpHazmatClient.GetHazmatSectionAsync(spSettings.SpecialProvisionSection);
            File.WriteAllText($"{title49ApiSettings.DataRoot}{title49ApiSettings.ReportDate}{dataFileSuffix}", rawData);

            PreprocessData(rawData);
        }

        switch (spSettings.Mode)
        {
            case ProcessModes.Load:
                break;

            case ProcessModes.Identify:
                break;

            case ProcessModes.Update:
                break;
            case ProcessModes.Clear:
                logger.LogWarning("Clearing Hazmat Items");
                await new ClearTables<SpecialProvision>(dbRepository, configuration).Clear();
                logger.LogWarning("Clearing Hazmat Items complete");
                return;
            default:
                logger.LogError("Process mode not set");
                return;
        }
        logger.LogWarning("Mode {mode} Starting", spSettings.Mode.ToString());
        return;
    }

    /// <summary>
    /// Split raw data into data processing sections and subsections.
    /// </summary>
    /// <param name="rawData">String retrieved for Hazmat Authority API service</param>
    private void PreprocessData(string rawData)
    {
        string pattern = @"<P>\(\d+\) <I>";

        if (string.IsNullOrEmpty(rawData))
        {
            logger.LogError("No data found in file {File}", $"{title49ApiSettings.DataRoot}{title49ApiSettings.ReportDate}SpecialProvisions.htm");
            return;
        }

        DataSections.AddRange(Regex.Split(rawData, pattern));

        int sectionIdx = -1;

        DataSections.ForEach(section =>
        {
            sectionIdx++;
            if (sectionIdx == 0)
            {
                // Skip the first section as it does not contain data.
                return;
            }   

            // Store off each section to process.
            File.WriteAllText($"{title49ApiSettings.DataRoot}SP_Section{sectionIdx}.txt", section);
        });
    }   

    private void DetermineProcessModeAsync(string sectionData)
    {
    }
}
