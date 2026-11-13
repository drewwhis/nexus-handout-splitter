using System.CommandLine;
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
  private const string BaseUrlFtc = "https://ftc-api.firstinspires.org";
  private const string BaseUrlFrc = "https://frc-api.firstinspires.org";
  private const string FtcCredentialsFile = "./secrets/FTC_API_KEY.txt";
  private const string FrcCredentialsFile = "./secrets/FRC_API_KEY.txt";

  public static async Task Main(string[] args)
  {
    PdfSharpLicense.SetAgplLicense();

    RootCommand command = new("Split Nexus handouts");
    Option<FileInfo> fileOption = new("--file", "-f")
    {
      Description = "The Nexus handouts file",
      Required = true
    };
    
    Option<string> eventCodeOption = new("--event", "-e")
    {
      Description = "The event code",
      Required = true
    };

    Option<string> programOption = new("--program", "-p")
    {
      Description = "The program code (FRC, FTC)",
      Required = true
    };

    Option<int> firstSeasonOption = new("--firstseason", "-s")
    {
      Description = "The season year of the FIRST season",
      Required = true
    };
    
    command.Options.Add(fileOption);
    command.Options.Add(eventCodeOption);
    command.Options.Add(programOption);
    command.Options.Add(firstSeasonOption);

    command.SetAction(async parseResult =>
    {
      var file = parseResult.GetValue(fileOption);
      if (file is null)
      {
        Console.WriteLine($"No Nexus handouts file found");
        return 1;
      }
      
      var eventCode = parseResult.GetValue(eventCodeOption);
      if (string.IsNullOrWhiteSpace(eventCode))
      {
        Console.WriteLine($"No event code specified");
        return 2;
      }
      
      var programCode = parseResult.GetValue(programOption)?.ToUpperInvariant();
      if (string.IsNullOrWhiteSpace(programCode))
      {
        Console.WriteLine($"No program code specified");
        return 3;
      }

      if (programCode != "FRC" && programCode != "FTC")
      {
        Console.WriteLine($"Invalid program code specified {programCode}");
        return 3;
      }
      
      var season =  parseResult.GetValue(firstSeasonOption);
      if (season < 2027)
      {
        Console.WriteLine($"Season {season} is invalid");
        return 4;
      }
      
      var teamNumbers = (await GetTeamList(programCode, season, eventCode)).ToList();
      if (teamNumbers.Count == 0)
      {
        Console.WriteLine("No teams found");
        return 5;
      }
      
      CreateHandouts(teamNumbers, file.FullName);
      
      foreach (var teamNumber in teamNumbers)
      {
        Console.WriteLine(teamNumber);
      }

      return 0;
    });
    
    await command.Parse(args).InvokeAsync();
  }

  private static async Task<IEnumerable<int>> GetTeamList(string programCode, int firstSeason, string eventCode)
  {
    var isFrc = programCode == "FRC";
    var version = isFrc ? 3 : 2;
    var season = isFrc ? firstSeason : firstSeason - 1;
    var baseUrl = isFrc ? BaseUrlFrc : BaseUrlFtc;
    var credentialPath = isFrc ? FrcCredentialsFile : FtcCredentialsFile;
    
    using var client = new HttpClient();
    client.BaseAddress = new Uri(baseUrl);

    var endpoint = $"v{version}.0/{season}/teams";
    var queryParams = new Dictionary<string, string?>
    {
      ["eventCode"] = eventCode,
    };
    var url = QueryHelpers.AddQueryString(endpoint, queryParams);

    string credentials;
    try
    {
      credentials = await File.ReadAllTextAsync(credentialPath);
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
  
  private static void CreateHandouts(IEnumerable<int> teams, string filePath)
  {
    var teamsList = teams.ToList();
    var inputDocument = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
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
        outputDocument.Save($"Team_{teamsList[teamIndex]}.pdf");
      }
    }
  }
}



