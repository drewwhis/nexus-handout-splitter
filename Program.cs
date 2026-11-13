using pdf_split.API;
using PdfSharp;

namespace pdf_split;

using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

public abstract class Program
{
  private const string HandoutsFileName = "2026usalorq_handouts.pdf";
  private const string EventCode = "USALORQ";
  private const string BaseUrlFtc = "https://ftc-api.firstinspires.org";
  private const int FirstSeason = 2027;
  private const string FtcCredentialsFile = "../../../secrets/FTC_API_KEY.txt";

  public static async Task Main()
  {
    PdfSharpLicense.SetAgplLicense();
    var teamNumbers = (await GetFtcTeamList()).ToList();
    if (teamNumbers.Count == 0)
    {
      Console.WriteLine("No teams found");
      return;
    }
    
    CreateHandouts(teamNumbers);
    foreach (var teamNumber in teamNumbers)
    {
      Console.WriteLine(teamNumber);
    }
  }

  private static async Task<IEnumerable<int>> GetFtcTeamList()
  {
    using var client = new HttpClient();
    client.BaseAddress = new Uri(BaseUrlFtc);

    var endpoint = $"v2.0/{FirstSeason - 1}/teams";
    var queryParams = new Dictionary<string, string?>
    {
      ["eventCode"] = EventCode,
    };
    var url = QueryHelpers.AddQueryString(endpoint, queryParams);

    string credentials;
    try
    {
      credentials = await File.ReadAllTextAsync(FtcCredentialsFile);
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex.Message);
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
    var inputDocument = PdfReader.Open($"../../../{HandoutsFileName}", PdfDocumentOpenMode.Import);
    var height = XUnit.FromInch(3.66);
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
            new XPoint(0, page.Height.Point - height.Point * (j + 1)),
            new XSize(page.Width.Point, height.Point)
        );

        // Add the page and save it
        outputDocument.AddPage(inputDocument.Pages[i]);
        outputDocument.Save($"../../../Team_{teamsList[teamIndex]}.pdf");
      }
    }
  }
}



