
using NReco.VideoConverter;

var convertVideo = new NReco.VideoConverter.FFMpegConverter();

var input = "F:\\Projects\\test.oga";
var outputFile = "F:\\Projects\\test.mp3";

convertVideo.ConvertMedia(input, outputFile, "mp3");

Console.WriteLine("Done");