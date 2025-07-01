
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;
using TestDockerApp.Models;


class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AppSettings>(context.Configuration.GetSection("AppSettings"));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole(); // Logs to stdout/stderr
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var appSettings = config.GetSection("AppSettings").Get<AppSettings>();

        logger.LogInformation("Application started.");

        string token = await GetAuthToken(logger);
        if (!string.IsNullOrEmpty(token))
        {
            logger.LogInformation("Token retrieved successfully.");
            string jsonResponse = await GetJobPostings(token, logger);
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                logger.LogInformation("Job postings retrieved.");
                ConvertJsonToXmlAndSave(jsonResponse, appSettings.XmlOutputPath, logger);
                logger.LogInformation("XML saved.");
            }
        }
        else
        {
            logger.LogError("Failed to retrieve token.");
        }

        logger.LogInformation("Application ended.");
    }

    static async Task<string> GetAuthToken(ILogger logger)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://soundphysicians--uat.sandbox.my.salesforce.com/services/oauth2/token");
        var content = new MultipartFormDataContent
        {
            { new StringContent("client_credentials"), "grant_type" },
            { new StringContent("3MVG9wLr_6EJB3zD3vx39tIYkT_ACw_V92XVgBu4y0GuYoHoBXFKW3h9Unwox8vuR8aoEfXyFvtMuI9gwcEle"), "client_id" },
            { new StringContent("86CBDCB1319170A8E910ADEED8951C71F77E6603827EE5337D19BDBA002FBF83"), "client_secret" }
        };

        request.Content = content;

        var response = await client.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var json = JObject.Parse(responseString);
            return json["access_token"]?.ToString();
        }
        else
        {
            logger.LogError("Failed to get token: {Response}", responseString);
            return null;
        }
    }

    static async Task<string> GetJobPostings(string token, ILogger logger)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("https://soundphysicians--uat.sandbox.my.salesforce.com/services/apexrest/SoundCareers/all");
        var responseString = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return responseString;
        }
        else
        {
            logger.LogError("Failed to get job postings: {Response}", responseString);
            return null;
        }
    }

    static void ConvertJsonToXmlAndSave(string json, string filePath, ILogger logger)
    {
        try
        {
            JArray jsonArray = JArray.Parse(json);
            JObject wrappedJson = new JObject { ["Root"] = jsonArray };
            XmlDocument doc = JsonConvert.DeserializeXmlNode(wrappedJson.ToString(), "Roots");
            doc.Save(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError("Error converting JSON to XML: {Message}", ex.Message);
        }
    }
}
