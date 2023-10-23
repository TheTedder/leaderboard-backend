using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LeaderboardBackend.Models.Entities;

public class ApplicationContext : DbContext
{
    public const string CASE_INSENSITIVE_COLLATION = "case_insensitive";

    private readonly AppContextDataSourceProvider _dataSourceProvider;

    public ApplicationContext(DbContextOptions<ApplicationContext> options, AppContextDataSourceProvider dataSourceProvider)
        : base(options)
    {
        _dataSourceProvider = dataSourceProvider;
    }

    public DbSet<AccountRecovery> AccountRecoveries { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<AccountConfirmation> AccountConfirmations { get; set; } = null!;
    public DbSet<Leaderboard> Leaderboards { get; set; } = null!;
    public DbSet<Run> Runs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    public static NpgsqlDataSource CreateDataSource(PostgresConfig config)
    {
        NpgsqlConnectionStringBuilder connectionBuilder = new()
        {
            Host = config.Host,
            Username = config.User,
            Password = config.Password,
            Database = config.Db,
            IncludeErrorDetail = true,
        };

        if (config.Port is not null)
        {
            connectionBuilder.Port = config.Port.Value;
        }

        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionBuilder.ConnectionString);
        dataSourceBuilder.UseNodaTime().MapEnum<UserRole>();

        return dataSourceBuilder.Build();
    }

    public void MigrateDatabase()
    {
        Database.Migrate();
        NpgsqlConnection connection = (NpgsqlConnection)Database.GetDbConnection();
        connection.Open();

        try
        {
            connection.ReloadTypes();
        }
        finally
        {
            connection.Close();
        }
    }

    /// <summary>
    /// Migrates the database and reloads Npgsql types
    /// </summary>
    public async Task MigrateDatabaseAsync()
    {
        await Database.MigrateAsync();

        // when new extensions have been enabled by migrations, Npgsql's type cache must be refreshed
        NpgsqlConnection connection = (NpgsqlConnection)Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await connection.ReloadTypesAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.HasCollation(CASE_INSENSITIVE_COLLATION, "und-u-ks-level2", "icu", deterministic: false);
        modelBuilder.HasPostgresEnum<UserRole>();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder opt)
    {
        opt.UseNpgsql(_dataSourceProvider.Value, o => o.UseNodaTime());
        opt.UseSnakeCaseNamingConvention();
    }
}

public class AppContextDataSourceProvider
{
    private static int _cacheKey;
    private static NpgsqlDataSource? _cachedDataSource;

    public NpgsqlDataSource Value => _cachedDataSource!;

    public AppContextDataSourceProvider(IOptions<ApplicationContextConfig> appContextConfig)
    {
        PostgresConfig config = appContextConfig.Value.Pg;

        int key = config.GetHashCode(); // a record's HashCode is calculated with all its properties values
        if (_cacheKey != key)
        {
            // if we ever want to parallelize tests, this code will need to be made thread-safe
            _cacheKey = key;
            _cachedDataSource = ApplicationContext.CreateDataSource(config);
        }
    }
}
