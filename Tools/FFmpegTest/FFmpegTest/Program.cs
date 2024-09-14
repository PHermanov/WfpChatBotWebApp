using FFMpegCore;
using FFMpegCore.Pipes;

var output = new MemoryStream();

FFMpegArguments
    .FromFileInput("F:\\Projects\\test.tmp", true, options => options.WithCustomArgument("-v 48"))
    .OutputToPipe(new StreamPipeSink(output), options => options.ForceFormat("mp3"))
    .ProcessSynchronously(true);

output.Position = 0;
using var fileStream = new FileStream("result.mp3", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
output.CopyTo(fileStream);
