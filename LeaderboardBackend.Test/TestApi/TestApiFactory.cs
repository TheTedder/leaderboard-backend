using System;
using LeaderboardBackend.Models.Entities;
using LeaderboardBackend.Test.Lib;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using BCryptNet = BCrypt.Net.BCrypt;

namespace LeaderboardBackend.Test.TestApi;

public class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly Mock<ISmtpClient> _mock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Staging);

        if (PostgresDatabaseFixture.PostgresContainer is null)
        {
            throw new InvalidOperationException("Postgres container is not initialized.");
        }

        Environment.SetEnvironmentVariable("ApplicationContext__PG__DB", PostgresDatabaseFixture.Database);
        Environment.SetEnvironmentVariable("ApplicationContext__PG__PORT", PostgresDatabaseFixture.Port.ToString());
        Environment.SetEnvironmentVariable("ApplicationContext__PG__HOST", PostgresDatabaseFixture.PostgresContainer.Hostname);
        Environment.SetEnvironmentVariable("ApplicationContext__PG__USER", PostgresDatabaseFixture.Username);
        Environment.SetEnvironmentVariable("ApplicationContext__PG__PASSWORD", PostgresDatabaseFixture.Password);

        // builder.ConfigureAppConfiguration(configBuilder =>
        // {
        //     MemoryConfigurationSource memoryConfigurationSource = new()
        //     {
        //         InitialData = new Dictionary<string, string>
        //         {
        //             ["ApplicationContext__Pg__Db"] = PostgresDatabaseFixture.Database!,
        //             ["ApplicationContext__Pg__Port"] = PostgresDatabaseFixture.Port.ToString(),
        //             ["Applicationcontext__Pg__Host"] = PostgresDatabaseFixture.PostgresContainer!.Hostname,
        //             ["ApplicationContext__Pg__User"] = PostgresDatabaseFixture.Username!,
        //             ["ApplicationContext__Pg__Password"] = PostgresDatabaseFixture.Password!
        //         }!
        //     };

        //     configBuilder.Add(memoryConfigurationSource);
        // });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Transient(_ => _mock.Object));

            using AsyncServiceScope scope = services.BuildServiceProvider().CreateAsyncScope();
            ApplicationContext dbContext =
                scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            InitializeDatabase(dbContext);
        });
    }

    // protected override void ConfigureWebHost(IWebHostBuilder builder)
    // {
    //     // Set the environment for the run to Staging
    //     builder.UseEnvironment(Environments.Staging);
    //     //base.ConfigureWebHost(builder);

    //     builder.ConfigureAppConfiguration((context, configBuilder) =>
    //     {
    //         configBuilder.AddInMemoryCollection(new Dictionary<string, string>
    //         {
    //             ["ApplicationContext:Pg:Db"] = PostgresDatabaseFixture.Database!,
    //             ["ApplicationContext:Pg:Port"] = PostgresDatabaseFixture.Port.ToString(),
    //             ["Applicationcontext:Pg:Host"] = PostgresDatabaseFixture.PostgresContainer!.Hostname,
    //             ["ApplicationContext:Pg:User"] = PostgresDatabaseFixture.Username!,
    //             ["ApplicationContext:Pg:Password"] = PostgresDatabaseFixture.Password!
    //         }!);
    //     });

    //     builder.ConfigureServices(services =>
    //     {
    //         if (PostgresDatabaseFixture.PostgresContainer is null)
    //         {
    //             throw new InvalidOperationException("Postgres container is not initialized.");
    //         }

    //         // services.Configure<ApplicationContextConfig>(conf =>
    //         //     conf.Pg = new PostgresConfig
    //         //     {
    //         //         Db = PostgresDatabaseFixture.Database!,
    //         //         Port = (ushort)PostgresDatabaseFixture.Port,
    //         //         Host = PostgresDatabaseFixture.PostgresContainer.Hostname,
    //         //         User = PostgresDatabaseFixture.Username!,
    //         //         Password = PostgresDatabaseFixture.Password!
    //         //     });

    //         services.Replace(ServiceDescriptor.Transient(_ => _mock.Object));

    //         using IServiceScope scope = services.BuildServiceProvider().CreateScope();
    //         ApplicationContext dbContext =
    //             scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    //         InitializeDatabase(dbContext);
    //     });
    // }

    public TestApiClient CreateTestApiClient() => new(CreateClient());

    private static void InitializeDatabase(ApplicationContext dbContext)
    {
        if (!PostgresDatabaseFixture.HasCreatedTemplate)
        {
            dbContext.MigrateDatabase();
            Seed(dbContext);
            PostgresDatabaseFixture.CreateTemplateFromCurrentDb();
        }
    }

    private static void Seed(ApplicationContext dbContext)
    {
        Leaderboard leaderboard =
            new() { Name = "Mario Goes to Jail", Slug = "mario-goes-to-jail" };

        User admin =
            new()
            {
                //Id = TestInitCommonFields.Admin.Id,
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
    public void ResetDatabase()
    {
        PostgresDatabaseFixture.ResetDatabaseToTemplate();
    }
}
