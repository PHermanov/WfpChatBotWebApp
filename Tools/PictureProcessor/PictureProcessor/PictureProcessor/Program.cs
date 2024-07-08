using System.Diagnostics;

var imageStream = await PictureProcessor.PictureProcessor.GetWinnerImageMonth();

var fileName = $"test_{DateTime.Now:dd-MM-HH-mm-ss}.png";

var fileStream = File.Create(fileName);
imageStream.WriteTo(fileStream);
fileStream.Close();
imageStream.Close();




