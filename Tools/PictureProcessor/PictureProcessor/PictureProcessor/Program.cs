using System.Diagnostics;
using System.Globalization;

//var imageStream = await PictureProcessor.PictureProcessor.GetWinnerImageMonth();
var imageStream = await PictureProcessor.PictureProcessor.GetWinnerImageYear(2024);

var fileName = $"test_{DateTime.Now.ToString("dd-MM-HH-mm-ss", CultureInfo.InvariantCulture)}.png";

var fileStream = File.Create(fileName);
imageStream.WriteTo(fileStream);
fileStream.Close();
imageStream.Close();




