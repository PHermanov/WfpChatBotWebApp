using FFMpegCore;
using FFMpegCore.Pipes;

var output = new MemoryStream();

FFMpegArguments
    .FromFileInput("F:\\Projects\\test.tmp")
    .OutputToPipe(new StreamPipeSink(output))
    .ProcessSynchronously();

using var fileStream = new FileStream("result.mp3", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
output.CopyTo(fileStream);