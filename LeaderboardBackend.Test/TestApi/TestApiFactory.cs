using System;
using System.Net.Http;
using System.Threading.Tasks;
using LeaderboardBackend.Models.Entities;
using LeaderboardBackend.Services;
using LeaderboardBackend.Test.Lib;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using BCryptNet = BCrypt.Net.BCrypt;

namespace LeaderboardBackend.Test.TestApi;

public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            if (PostgresDatabaseFixture.PostgresContainer is null)
            {
                throw new InvalidOperationException("Postgres container is not initialized.");
            }

            services.Configure<ApplicationContextConfig>(conf =>
                conf.Pg = new PostgresConfig
                {
                    Db = PostgresDatabaseFixture.Database!,
                    Port = (ushort)PostgresDatabaseFixture.Port,
                    Host = PostgresDatabaseFixture.PostgresContainer.Hostname,
                    User = PostgresDatabaseFixture.Username!,
                    Password = PostgresDatabaseFixture.Password!
                }
            );

            services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IEmailSender>().Object));

            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            ApplicationContext dbContext =
                scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            InitializeDatabase(dbContext);
        });
    }

    public TestApiClient CreateTestApiClient()
    {
        HttpClient client = CreateClient();
        return new TestApiClient(client);
    }

    public static void InitializeDatabase(ApplicationContext dbContext)
    {
        if (!PostgresDatabaseFixture.IsInitialized)
        {
            dbContext.Database.Migrate();
            Seed(dbContext);
            PostgresDatabaseFixture.IsInitialized = true;
        }
    }

    private static void Seed(ApplicationContext dbContext)
    {
        Leaderboard leaderboard =
            new() { Name = "Mario Goes to Jail", Slug = "mario-goes-to-jail" };

        User admin =
            new()
            {
                Id = TestInitCommonFields.Admin.Id,
                Username = TestInitCommonFields.Admin.Username,
                Email = TestInitCommonFields.Admin.Email,
                Password = BCryptNet.EnhancedHashPassword(TestInitCommonFields.Admin.Password),
                Role = UserRole.Administrator,
            };

        dbContext.Add(admin);
        dbContext.Add(leaderboard);
        dbContext.SaveChanges();
    }

    /// <summary>
    /// Deletes and recreates the database
    /// </summary>
    public static async Task ResetDatabase(ApplicationContext context)
    {
        await context.Runs.ExecuteDeleteAsync();
        await context.AccountConfirmations.ExecuteDeleteAsync();
        await context.AccountRecoveries.ExecuteDeleteAsync();
        await context.Categories.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
        await context.Leaderboards.ExecuteDeleteAsync();
        Seed(context);
    }
}
