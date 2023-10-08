using System;
using System.Net.Http;
using System.Threading.Tasks;
using LeaderboardBackend.Models.Entities;
using LeaderboardBackend.Test.Lib;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using BCryptNet = BCrypt.Net.BCrypt;

namespace LeaderboardBackend.Test.TestApi;

public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the environment for the run to Staging
        builder.UseEnvironment(Environments.Staging);

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(async services =>
        {
            if (PostgresDatabaseFixture.PostgresContainer is null)
            {
                throw new InvalidOperationException("Postgres container is not initialized.");
            }

            services.Configure<ApplicationContextConfig>(conf =>
            {
                conf.Pg = new PostgresConfig
                {
                    Db = PostgresDatabaseFixture.Database!,
                    Port = (ushort)PostgresDatabaseFixture.Port,
                    Host = PostgresDatabaseFixture.PostgresContainer.Hostname,
                    User = PostgresDatabaseFixture.Username!,
                    Password = PostgresDatabaseFixture.Password!
                };
            });

            // mock SMTP client
            services.Replace(ServiceDescriptor.Transient<ISmtpClient>(_ => new Mock<ISmtpClient>().Object));

            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            ApplicationContext dbContext =
                scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            await InitializeDatabase(dbContext);
        });
    }

    public TestApiClient CreateTestApiClient()
    {
        HttpClient client = CreateClient();
        return new TestApiClient(client);
    }

    public async Task InitializeDatabase()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        await InitializeDatabase(dbContext);
    }

    private static async Task InitializeDatabase(ApplicationContext dbContext)
    {
        if (!PostgresDatabaseFixture.HasCreatedTemplate)
        {
            await dbContext.MigrateDatabase();
            await Seed(dbContext);
            await PostgresDatabaseFixture.CreateTemplateFromCurrentDb();
        }
    }

    private static async Task Seed(ApplicationContext dbContext)
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

        await dbContext.Users.AddAsync(admin);
        await dbContext.Leaderboards.AddAsync(leaderboard);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes and recreates the database
    /// </summary>
    public async Task ResetDatabase()
    {
        await PostgresDatabaseFixture.ResetDatabaseToTemplate();
    }
}
