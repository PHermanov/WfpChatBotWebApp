using FFMpegCore;
using FFMpegCore.Pipes;

var output = new MemoryStream();

FFMpegArguments
    .FromFileInput("F:\\Projects\\test.tmp", true, options => options.WithCustomArgument("-v 48"))
    .OutputToPipe(new StreamPipeSink(output), options => options.ForceFormat("wav"))
    .ProcessSynchronously(true);

using var fileStream = new FileStream("result.mp3", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
output.CopyTo(fileStream);
fileStream.Close();