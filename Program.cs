// See https://aka.ms/new-console-template for more information
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

var pathToHandouts = "";
var inputDocument = PdfReader.Open(pathToHandouts, PdfDocumentOpenMode.Import);

var teamsList = new List<int>{};

var height = XUnit.FromInch(3.66).Point;
for (var i = 0; i < inputDocument.PageCount; i++)
{
  var page = inputDocument.Pages[i];

  for (var j = 0; j < 3; j++)
  {
    var teamIndex = i * 3 + j;
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
