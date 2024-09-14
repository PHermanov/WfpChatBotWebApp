using FFMpegCore;

FFMpegArguments
    .FromUrlInput(new Uri("https://demo.twilio.com/docs/classic.mp3"))
    .OutputToFile("classic.wav")
    .ProcessSynchronously();