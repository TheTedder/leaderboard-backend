using System.Net.Http;
using System.Threading.Tasks;
using LeaderboardBackend.Test.TestApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NUnit.Framework;

namespace LeaderboardBackend.Test.Fixtures;
public abstract class IntegrationTestsBase
{
    protected HttpClient Client { get; private set; } = null!;

    protected static readonly TestApiFactory _factory = new();

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await _factory.InitializeDatabase();
    }

    [SetUp]
    public void SetUp()
    {
        Client = _factory.CreateClient();
    }

    protected void SetClientBearer(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new(JwtBearerDefaults.AuthenticationScheme, token);
    }
}
