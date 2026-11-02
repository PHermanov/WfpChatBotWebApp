using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.Instagram;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class InstagramServiceTests
{
    private const string Cdn = "https://scontent.cdninstagram.com/v/";

    [Theory]
    [InlineData("look https://www.instagram.com/reel/DAbc_12-xY/?igsh=abc", "DAbc_12-xY")]
    [InlineData("https://instagram.com/p/BQ0eAlwhDrw/", "BQ0eAlwhDrw")]
    [InlineData("instagram.com/reels/CDUMkliABpa", "CDUMkliABpa")]
    [InlineData("https://www.instagram.com/some.user_1/reel/CDUMkliABpa/", "CDUMkliABpa")]
    [InlineData("https://m.instagram.com/tv/BkfuX9UB-eK", "BkfuX9UB-eK")]
    [InlineData("(https://instagr.am/p/aye83DjauH).", "aye83DjauH")]
    public void FindLinks_RecognizesReelAndPostLinks(string text, string shortcode)
    {
        var link = Assert.Single(InstagramLinkParser.FindLinks(new Message { Text = text }));
        Assert.True(InstagramLinkParser.TryGetShortcode(link, out var parsed));
        Assert.Equal(shortcode, parsed);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("https://www.instagram.com/someone/")]
    [InlineData("https://www.instagram.com/reels/audio/123456/")]
    [InlineData("https://ddinstagram.com/reel/CDUMkliABpa/")]
    [InlineData("https://www.instagram.com.evil.example/p/CDUMkliABpa/")]
    public void FindLinks_IgnoresNonPostLinks(string text) =>
        Assert.Empty(InstagramLinkParser.FindLinks(new Message { Text = text }));

    [Fact]
    public void FindLinks_ReadsHiddenTextLinksAndShareLinks()
    {
        var links = InstagramLinkParser.FindLinks(new Message
        {
            Text = "this and https://www.instagram.com/share/reel/BAabc123/",
            Entities = [new MessageEntity { Type = MessageEntityType.TextLink, Offset = 0, Length = 4, Url = "https://www.instagram.com/p/BQ0eAlwhDrw/" }]
        });

        Assert.Equal(2, links.Count);
        Assert.True(InstagramLinkParser.IsShareLink(links[0]));
        Assert.False(InstagramLinkParser.TryGetShortcode(links[0], out _));
        Assert.True(InstagramLinkParser.TryGetShortcode(links[1], out var shortcode));
        Assert.Equal("BQ0eAlwhDrw", shortcode);
    }

    [Fact]
    public void ParsePostPage_IgnoresOtherPostsAndUntrustedMediaHosts()
    {
        var html = PostPage("REEL1", new { media_type = 2, video_versions = new[] { new { url = "https://evil.example/reel1.mp4" } } },
            decoy: new { code = "OTHER", media_type = 2, video_versions = new[] { new { url = Cdn + "other.mp4" } } });

        Assert.Empty(InstagramMediaParser.ParsePostPage(html, "REEL1"));
    }

    [Fact]
    public async Task TrySendMedia_ResharesReelVideoFromPostPageAsReply()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/REEL1/" => Html(PostPage("REEL1", new
            {
                media_type = 2,
                video_versions = new[] { new { url = Cdn + "reel1.mp4?sig=a&x=1" }, new { url = Cdn + "reel1-low.mp4" } },
                image_versions2 = new { candidates = new[] { new { url = Cdn + "reel1.jpg" } } }
            }, decoy: new { code = "OTHER", media_type = 2, video_versions = new[] { new { url = Cdn + "other.mp4" } } })),
            Cdn + "reel1.mp4?sig=a&x=1" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://video.xx.fbcdn.net/o1/reel1.mp4?sig=a") } },
            "https://video.xx.fbcdn.net/o1/reel1.mp4?sig=a" => Bytes([1, 2, 3]),
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("watch https://www.instagram.com/reel/REEL1/?igsh=x"), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "https://www.instagram.com/p/REEL1/", Cdn + "reel1.mp4?sig=a&x=1", "https://video.xx.fbcdn.net/o1/reel1.mp4?sig=a" }, fake.Requests);
        var upload = Assert.Single(fake.Uploads);
        Assert.Equal("sendVideo", upload.Method);
        Assert.Equal(new byte[] { 1, 2, 3 }, upload.File("instagram_REEL1_1.mp4"));
        Assert.Contains("\"message_id\":42", upload.Text("reply_parameters"));
        Assert.Equal("true", upload.Text("supports_streaming"), ignoreCase: true);
    }

    [Fact]
    public async Task TrySendMedia_SendsCarouselAsMediaGroupInPostOrder()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/CAR1/" => Html(PostPage("CAR1", new
            {
                media_type = 8,
                image_versions2 = new { candidates = new[] { new { url = Cdn + "cover.jpg" } } },
                carousel_media = new object[]
                {
                    new { media_type = 2, video_versions = new[] { new { url = Cdn + "car1.mp4" } } },
                    new { media_type = 1, image_versions2 = new { candidates = new object[] { new { url = Cdn + "car2-full.jpg" }, new { url = Cdn + "car2-small.jpg", width = 320, height = 320 } } } }
                }
            })),
            Cdn + "car1.mp4" => Bytes([1]),
            Cdn + "car2-full.jpg" => Bytes([2]),
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/p/CAR1/"), TestContext.Current.CancellationToken);

        var upload = Assert.Single(fake.Uploads);
        Assert.Equal("sendMediaGroup", upload.Method);
        Assert.Equal(new byte[] { 1 }, upload.File("instagram_CAR1_1.mp4"));
        Assert.Equal(new byte[] { 2 }, upload.File("instagram_CAR1_2.jpg"));
        using var media = JsonDocument.Parse(upload.Text("media"));
        Assert.Equal(new[] { "video", "photo" }, media.RootElement.EnumerateArray().Select(item => item.GetProperty("type").GetString()).ToArray());
        Assert.DoesNotContain(Cdn + "cover.jpg", fake.Requests);
    }

    [Fact]
    public async Task TrySendMedia_FallsBackToEmbedPageWhenPostPageRedirectsToLogin()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/VID1/" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("/accounts/login/", UriKind.Relative) } },
            "https://www.instagram.com/p/VID1/embed/captioned/" => Html(EmbedPage(new
            {
                context = new { shortcode = "VID1" },
                gql_data = new { shortcode_media = new { __typename = "GraphVideo", shortcode = "VID1", is_video = true, video_url = "https://instagram.fxyz1-1.fna.fbcdn.net/v/vid1.mp4", display_url = Cdn + "thumb.jpg" } }
            })),
            "https://instagram.fxyz1-1.fna.fbcdn.net/v/vid1.mp4" => Bytes([9, 8]),
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/tv/VID1"), TestContext.Current.CancellationToken);

        var upload = Assert.Single(fake.Uploads);
        Assert.Equal("sendVideo", upload.Method);
        Assert.Equal(new byte[] { 9, 8 }, upload.File("instagram_VID1_1.mp4"));
    }

    [Fact]
    public async Task TrySendMedia_NeverSubstitutesThumbnailForUnavailableReelVideo()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/REEL2/" => Html(PostPage("REEL2", new { media_type = 2, image_versions2 = new { candidates = new[] { new { url = Cdn + "thumb.jpg" } } } })),
            "https://www.instagram.com/p/REEL2/embed/captioned/" => Html(EmbedPage(new
            {
                gql_data = new { shortcode_media = new { __typename = "GraphVideo", shortcode = "REEL2", is_video = true, display_url = Cdn + "thumb.jpg" } }
            })),
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/reel/REEL2/"), TestContext.Current.CancellationToken);

        Assert.Empty(fake.Uploads);
        Assert.DoesNotContain(Cdn + "thumb.jpg", fake.Requests);
    }

    [Fact]
    public async Task TrySendMedia_SkipsVideosAboveTelegramUploadLimit()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/BIG1/" => Html(PostPage("BIG1", new { media_type = 2, video_versions = new[] { new { url = Cdn + "big.mp4" } } })),
            Cdn + "big.mp4" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) { Headers = { ContentLength = 51L * 1024 * 1024 } } },
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/reel/BIG1/"), TestContext.Current.CancellationToken);

        Assert.Empty(fake.Uploads);
    }

    [Fact]
    public async Task TrySendMedia_RefusesMediaRedirectsOutsideInstagramCdn()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/p/EVIL1/" => Html(PostPage("EVIL1", new { media_type = 2, video_versions = new[] { new { url = Cdn + "evil1.mp4" } } })),
            Cdn + "evil1.mp4" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://169.254.169.254/latest") } },
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/reel/EVIL1/"), TestContext.Current.CancellationToken);

        Assert.Empty(fake.Uploads);
        Assert.DoesNotContain(fake.Requests, request => request.Contains("169.254.169.254"));
    }

    [Fact]
    public async Task TrySendMedia_ResolvesShareLinksThroughRedirect()
    {
        var fake = new InstagramFake(uri => uri.ToString() switch
        {
            "https://www.instagram.com/share/reel/BAabc123/" => new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri("https://www.instagram.com/reel/REEL3/?igsh=x") } },
            "https://www.instagram.com/p/REEL3/" => Html(PostPage("REEL3", new { media_type = 2, video_versions = new[] { new { url = Cdn + "reel3.mp4" } } })),
            Cdn + "reel3.mp4" => Bytes([3]),
            _ => null
        });

        await fake.Service.TrySendMediaAsync(Message("https://www.instagram.com/share/reel/BAabc123/"), TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 3 }, Assert.Single(fake.Uploads).File("instagram_REEL3_1.mp4"));
    }

    [Fact]
    public async Task Routing_ResharesInstagramLinksInsteadOfAutoReplyingAndStillMentionsAll()
    {
        List<string> calls = [];
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/getMe", request.RequestUri!.AbsolutePath);
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { id = 123456, is_bot = true, first_name = "Bot", username = "test_bot" } }));
        });
        using var client = new HttpClient(handler);
        var repository = TestProxy.Create<IGameRepository>((method, _) => method!.Name switch
        {
            nameof(IGameRepository.CheckUserAsync) => Task.CompletedTask,
            nameof(IGameRepository.IsInstagramEnabledAsync) => Task.FromResult(true),
            _ => throw new InvalidOperationException($"Unexpected repository call {method.Name}")
        });
        var instagram = TestProxy.Create<IInstagramService>((method, _) =>
        {
            calls.Add(method!.Name);
            return method.Name == nameof(IInstagramService.ContainsInstagramUrl) ? true : Task.CompletedTask;
        });
        var autoReply = TestProxy.Create<IAutoReplyService>((method, _) =>
        {
            calls.Add(method!.Name);
            return Task.CompletedTask;
        });
        var sut = new TelegramBotService(
            TestProxy.Create<IMediator>((_, _) => throw new InvalidOperationException("Unexpected command")),
            repository,
            autoReply,
            new TelegramBotClient("123456:test-key", client),
            TestProxy.Create<IBotReplyService>((_, _) => throw new InvalidOperationException("Unexpected AI reply")),
            instagram,
            TestProxy.Create<IThrottlingService>((_, _) => throw new InvalidOperationException("Unexpected throttling")),
            NullLogger<TelegramBotService>.Instance);

        await sut.HandleUpdateAsync(new Update { Message = Message("@all https://www.instagram.com/reel/REEL1/") }, TestContext.Current.CancellationToken);

        Assert.Equal([nameof(IInstagramService.ContainsInstagramUrl), nameof(IInstagramService.TrySendMediaAsync), nameof(IAutoReplyService.MentionAllAsync)], calls);
    }

    private static Message Message(string text) => new()
    {
        Id = 42, Chat = new Chat { Id = 10 }, From = new User { Id = 7, FirstName = "Alice" }, Text = text
    };

    private static string PostPage(string shortcode, object media, object? decoy = null)
    {
        var decoyScript = decoy is null ? string.Empty : $"<script type=\"application/json\" data-sjs>{JsonSerializer.Serialize(new { require = new[] { decoy } })}</script>";
        var data = new { xig_polaris_media = new { __typename = "XIGPolarisVideoMedia", code = shortcode, if_not_gated_logged_out = media } };
        return $"<html><head>{decoyScript}<script type=\"application/json\" nonce=\"n\" data-content-len=\"1\" data-sjs>{JsonSerializer.Serialize(new { require = new object[] { new { __bbox = new { result = new { data } } } } })}</script></head></html>";
    }

    private static string EmbedPage(object context)
    {
        var relaxed = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var init = JsonSerializer.Serialize(new { isRichEmbed = true, contextJSON = JsonSerializer.Serialize(context, relaxed) }, relaxed);
        return $"<html><script>requireLazy([\"PolarisEmbedSimple\",\"init\",[],[{init}]]);</script></html>";
    }

    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed record TelegramUpload(string Method, List<(string Name, string? FileName, byte[] Body)> Parts)
    {
        public byte[] File(string fileName) => Assert.Single(Parts, part => part.FileName == fileName).Body;
        public string Text(string name) => Encoding.UTF8.GetString(Assert.Single(Parts, part => part.Name == name).Body);
    }

    private sealed class InstagramFake
    {
        public List<string> Requests { get; } = [];
        public List<TelegramUpload> Uploads { get; } = [];
        public InstagramService Service { get; }

        public InstagramFake(Func<Uri, HttpResponseMessage?> instagram)
        {
            var handler = new ImageHttpHandler(async (request, token) =>
            {
                var uri = request.RequestUri!;
                if (uri.Host != "api.telegram.org")
                {
                    Requests.Add(uri.ToString());
                    return instagram(uri) ?? new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                List<(string, string?, byte[])> parts = [];
                foreach (var part in Assert.IsType<MultipartFormDataContent>(request.Content))
                    parts.Add((part.Headers.ContentDisposition!.Name!.Trim('"'), part.Headers.ContentDisposition.FileName?.Trim('"'), await part.ReadAsByteArrayAsync(token)));
                var method = uri.Segments[^1];
                Uploads.Add(new TelegramUpload(method, parts));
                var sent = new { message_id = 100, date = 0, chat = new { id = 10, type = "private" } };
                return method == "sendMediaGroup" ? ImageTestData.Json(new { ok = true, result = new[] { sent } }) : ImageTestData.Json(new { ok = true, result = sent });
            });
            Service = new InstagramService(new TelegramBotClient("123456:test-key", new HttpClient(handler)), new ImageHttpFactory(handler),
                NullLogger<InstagramService>.Instance);
        }
    }
}
