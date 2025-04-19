using System;
using Hazmat.Utilities.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hazmat.Utilities.Clients;

public class ECFRClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration configuration;

    public ECFRClient(IHttpClientFactory httpClientFactory,
                      ILogger<ECFRClient> logger,
                      IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("ECFRClient");
        this.configuration = configuration;
    }

    public async Task<string> GetHazmatSectionAsync(string sectionId)
    {
        Title49ApiSettings? hazmatConfig = configuration.GetSection("Title49Api").Get<Title49ApiSettings>();
        if (hazmatConfig == null)
        {
            throw new Exception("Title49Api configuration section is missing.");
        }
        var response = await _httpClient.GetAsync($"{hazmatConfig.UrlSuffix}{sectionId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

}
