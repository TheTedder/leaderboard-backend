using System;
using System.Threading.Tasks;
using LeaderboardBackend.Test.Fixtures;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;

// Fixtures apply to all tests in its namespace
// It has no namespace on purpose, so that the fixture applies to all tests in this assembly

[SetUpFixture] // https://docs.nunit.org/articles/nunit/writing-tests/attributes/setupfixture.html
internal class PostgresDatabaseFixture
{
    public static PostgreSqlContainer? PostgresContainer { get; private set; }
    public static string? Database { get; private set; }
    public static string? Username { get; private set; }
    public static string? Password { get; private set; }
    public static int Port { get; private set; }
    public static bool HasCreatedTemplate { get; private set; } = false;
    private static string TemplateDatabase => Database! + "_template";
    private static NpgsqlDataSource? _templateSource;

    [OneTimeSetUp]
    public static async Task OneTimeSetup()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithTmpfsMount("/var/lib/postgresql/data") // db files in-memory
            .Build();

        await PostgresContainer.StartAsync();
        NpgsqlConnectionStringBuilder connStrBuilder = new(PostgresContainer.GetConnectionString());
        Username = connStrBuilder.Username!;
        Password = connStrBuilder.Password!;
        Database = connStrBuilder.Database!;
        Port = connStrBuilder.Port;

        NpgsqlConnectionStringBuilder connStrBuilderTemplate =
            new(PostgresContainer.GetConnectionString()) { Database = "template1" };
        _templateSource = NpgsqlDataSource.Create(connStrBuilder);
    }

    [OneTimeTearDown]
    public static async Task OneTimeTearDown()
    {
        if (PostgresContainer is null)
        {
            return;
        }

        await PostgresContainer.DisposeAsync();
    }

    public static async Task CreateTemplateFromCurrentDb()
    {
        ThrowIfNotInitialized();
        NpgsqlConnection.ClearAllPools(); // can't drop a DB if connections remain open
        await using NpgsqlConnection conn = await TemplateDataSource().OpenConnectionAsync();

        await using NpgsqlCommand cmd = new(
                @$"
			DROP DATABASE IF EXISTS {TemplateDatabase};
			CREATE DATABASE {TemplateDatabase}
				WITH TEMPLATE {Database}
				OWNER '{Username}';
			"
            , conn);

        await cmd.ExecuteNonQueryAsync();
        HasCreatedTemplate = true;
    }

    // It is faster to recreate the db from an already seeded template
    // compared to dropping the db and recreating it from scratch
    public static async Task ResetDatabaseToTemplate()
    {
        ThrowIfNotInitialized();
        if (!HasCreatedTemplate)
        {
            throw new InvalidOperationException("Database template has not been created.");
        }

        NpgsqlConnection.ClearAllPools(); // can't drop a DB if connections remain open
        await using NpgsqlConnection conn = await TemplateDataSource().OpenConnectionAsync();

        await using NpgsqlCommand cmd = new(
                @$"
			DROP DATABASE IF EXISTS {Database};
			CREATE DATABASE {Database}
				WITH TEMPLATE {TemplateDatabase}
				OWNER '{Username}';
			"
            , conn);

        await cmd.ExecuteNonQueryAsync();
    }

    private static NpgsqlDataSource TemplateDataSource()
    {
        ThrowIfNotInitialized();
        return _templateSource ?? throw new InvalidOperationException("Template source has not been initialized");
    }

    private static void ThrowIfNotInitialized()
    {
        if (PostgresContainer is null)
        {
            throw new InvalidOperationException("Postgres container is not initialized.");
        }
    }
}
