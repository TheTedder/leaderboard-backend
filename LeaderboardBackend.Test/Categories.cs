using System.Net;
using System.Threading.Tasks;
using LeaderboardBackend.Models.Requests;
using LeaderboardBackend.Models.ViewModels;
using LeaderboardBackend.Test.Lib;
using LeaderboardBackend.Test.TestApi;
using LeaderboardBackend.Test.TestApi.Extensions;
using NUnit.Framework;

namespace LeaderboardBackend.Test;

[TestFixture]
public class Categories
{
    private TestApiClient _apiClient = null!;
    private TestApiFactory _factory = null!;
    private string? _jwt;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new();
        _apiClient = _factory.CreateTestApiClient();
        _jwt = (await _apiClient.LoginAdminUser()).Token;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [SetUp]
    public void Init()
    {
        _factory.ResetDatabase();
    }

    [Test]
    public void GetCategory_Unauthorized()
    {
        RequestFailureException e = Assert.ThrowsAsync<RequestFailureException>(
            async () => await _apiClient.Get<CategoryViewModel>($"/api/categories/1", new())
        )!;

        Assert.AreEqual(HttpStatusCode.Unauthorized, e.Response.StatusCode);
    }

    [Test]
    public void GetCategory_NotFound()
    {
        RequestFailureException e = Assert.ThrowsAsync<RequestFailureException>(
            async () =>
                await _apiClient.Get<CategoryViewModel>(
                    $"/api/categories/69",
                    new() { Jwt = _jwt }
                )
        )!;

        Assert.AreEqual(HttpStatusCode.NotFound, e.Response.StatusCode);
    }

    [Test]
    public async Task CreateCategory_GetCategory_OK()
    {
        LeaderboardViewModel createdLeaderboard = await _apiClient.Post<LeaderboardViewModel>(
            "/api/leaderboards",
            new()
            {
                Body = new CreateLeaderboardRequest()
                {
                    Name = Generators.GenerateRandomString(),
                    Slug = Generators.GenerateRandomString()
                },
                Jwt = _jwt
            }
        );

        CategoryViewModel createdCategory = await _apiClient.Post<CategoryViewModel>(
            "/api/categories",
            new()
            {
                Body = new CreateCategoryRequest()
                {
                    Name = Generators.GenerateRandomString(),
                    Slug = Generators.GenerateRandomString(),
                    LeaderboardId = createdLeaderboard.Id
                },
                Jwt = _jwt
            }
        );

        CategoryViewModel retrievedCategory = await _apiClient.Get<CategoryViewModel>(
            $"/api/categories/{createdCategory?.Id}",
            new() { Jwt = _jwt }
        );

        Assert.AreEqual(createdCategory, retrievedCategory);
    }
}
