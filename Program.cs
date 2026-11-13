using pdf_split.API;

namespace pdf_split;

using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

// See https://aka.ms/new-console-template for more information
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

public abstract class Program
{
  private const string PathToHandouts = "";
  private const string EventCode = "USALORQ";
  private const string BaseUrl = "https://ftc-api.firstinspires.org";
  private const int Season = 2026;
  private const string CredentialsFile = "./secrets/FTC_API_KEY.txt";

  public static async Task Main()
  {
    var teamNumbers = (await GetTeamsList()).ToList();
    if (!teamNumbers.Any())
    {
      Console.WriteLine("No teams found");
      return;
    }
    
    CreateHandouts(teamNumbers);
  }

  private static async Task<IEnumerable<int>> GetTeamsList()
  {
    using var client = new HttpClient();
    client.BaseAddress = new Uri(BaseUrl);

    var endpoint = $"v2.0/{Season}/teams";
    var queryParams = new Dictionary<string, string?>
    {
      ["eventCode"] = EventCode,
    };
    var url = QueryHelpers.AddQueryString(endpoint, queryParams);

    string credentials;
    try
    {
      credentials = await File.ReadAllTextAsync(CredentialsFile);
    }
    catch
    {
      return [];
    }
    
    var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64Credentials);

    try
    {
      var teams = await client.GetFromJsonAsync<TeamsResponse>(url);
      return teams is null ? [] : teams.Teams.Select(t => t.TeamNumber).Order();
    }
    catch (HttpRequestException ex)
    {
      Console.WriteLine($"Request error: {ex.Message}");
    }

    return [];
  }

  private static void CreateHandouts(IEnumerable<int> teams)
  {
    var teamsList = teams.ToList();
    var inputDocument = PdfReader.Open(PathToHandouts, PdfDocumentOpenMode.Import);
    var height = XUnit.FromInch(3.66).Point;
    var calculatedPageCount = (int)Math.Ceiling(teamsList.Count / 3.0);
    for (var i = 0; i < inputDocument.PageCount; i++)
    {
      var page = inputDocument.Pages[i];

      for (var j = 0; j < 3; j++)
      {
        var teamIndex = i + j * calculatedPageCount;
        if (teamIndex >= teamsList.Count) continue;

        var outputDocument = new PdfDocument
        {
          Version = inputDocument.Version
        };
        outputDocument.Info.Title = $"Team {teamsList[teamIndex]} of {inputDocument.Info.Title}";
        outputDocument.Info.Creator = inputDocument.Info.Creator;

        page.CropBox = new PdfRectangle(
            new XPoint(0, page.Height - height * (j + 1)),
            new XSize(page.Width, height)
        );

        // Add the page and save it
        outputDocument.AddPage(inputDocument.Pages[i]);
        outputDocument.Save($"Team_{teamsList[teamIndex]}.pdf");
      }
    }
  }
}



