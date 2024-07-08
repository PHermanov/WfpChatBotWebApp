using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PictureProcessor;

public static class PictureProcessor
{
    public static async Task<MemoryStream> GetWinnerImageMonth()
    {
        var bowlImage = await Image.LoadAsync("bowl.png");
        var winnerImageStream = new MemoryStream();

        using var avatarStream = new MemoryStream();
        await using var fileStream = File.Open("avatar.jpg", FileMode.Open);
        await fileStream.CopyToAsync(avatarStream);
        avatarStream.Position = 0;

        var avatarImage = await Image.LoadAsync(avatarStream);

        var width = avatarImage.Width;
        var height = avatarImage.Height;

        using var bitmap = new Image<Rgba32>(width, height);

        bitmap.Mutate(x => x.DrawImage(avatarImage, new Point(0, 0), 1f));

        // var hPadding = bitmap.Height / 10;
        // var vPadding = hPadding / 2;

        // add bowl image to avatar
        var bowlRatio = bitmap.Height / 2.2 / bowlImage.Height;
        var bowlWidth = (int)(bowlImage.Width * bowlRatio);
        var bowlHeight = (int)(bowlImage.Height * bowlRatio);

        bowlImage.Mutate(x => x.Resize(new Size(bowlWidth, bowlHeight)));

        var bowlPoint = new Point(bitmap.Width - bowlWidth - 10, bitmap.Height - bowlHeight - 10);

        bitmap.Mutate(x => x.DrawImage(bowlImage, bowlPoint, 1f));

        var font = SystemFonts.CreateFont("Impact", bowlHeight / 3.0f);

        // add date to avatar
        
        // long month name
        // var date = new DateTime(2024, 12, 31);

        // y char is going under the line
        var date = new DateTime(2024, 07, 31);
        
        var monthString = date.Date.ToString("MMMM", new System.Globalization.CultureInfo("en-US"));
        var yearString = date.Date.ToString("yyyy");

        var monthSize = TextMeasurer.MeasureBounds(monthString, new TextOptions(font));
        var monthPoint = new Point(
            20,
            height - (10 + bowlHeight / 2 + (int)monthSize.Height));

        bitmap.Mutate(x => x.DrawText(monthString, font, Brushes.Solid(Color.White),
            Pens.Solid(Color.Black, 2), monthPoint));

        var yearPoint = new Point(20, height - bowlHeight / 2);

        bitmap.Mutate(x => x.DrawText(yearString, font, Brushes.Solid(Color.White),
            Pens.Solid(Color.Black, 2), yearPoint));

        await bitmap.SaveAsync(winnerImageStream, new PngEncoder());

        winnerImageStream.Seek(0, SeekOrigin.Begin);

        return winnerImageStream;
    }
}