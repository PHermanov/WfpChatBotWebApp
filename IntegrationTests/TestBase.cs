using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection("Integration Tests")]
public abstract class TestBase : IAsyncLifetime
{
    private readonly TestServer _server;
    private AsyncServiceScope _scope;
    protected IServiceProvider Services;
    // protected HttpClient _client;

    public TestBase(TestServer server)
    {
        _server = server;
    }

    public async Task InitializeAsync()
    {
        // _client = _server.CreateClient(new WebApplicationFactoryClientOptions
        // {
        //     BaseAddress = new Uri("https://localhost/")
        // });
        _scope = _server.Services.CreateAsyncScope();
        Services = _scope.ServiceProvider;
    }

    // private Task ClearDatabaseAsync()
    // {
    //     var context = _services.GetRequiredService<AppDbContext>();
    // }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
    }
}