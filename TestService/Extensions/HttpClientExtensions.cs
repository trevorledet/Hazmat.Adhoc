using System;
using System.CodeDom;
using Hazmat.Utilities.Models;

namespace TestService.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddHttpHazmatClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("ECFRClient", client =>
        {
            Title49ApiSettings? title49Info = configuration.GetSection("Title49Api").Get<Title49ApiSettings>();
            if (title49Info == null)
            {
                throw new Exception("Title49Api configuration is missing.");
            }
            client.BaseAddress = new Uri($"{title49Info.BaseUrl}{title49Info.ReportDate}/");
            client.DefaultRequestHeaders.Add("Accept", "application/xml");
        });

        return services;
    }
}
