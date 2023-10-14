using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LeaderboardBackend.Models.Entities;

public class ApplicationContext : DbContext
{
    public const string CASE_INSENSITIVE_COLLATION = "case_insensitive";

    // the HashCode is calculated with all of the config's property because PostgresConfig is a record
    private static readonly ConcurrentDictionary<PostgresConfig, NpgsqlDataSource> _dataSourceCache = new();

    private readonly NpgsqlDataSource _dataSource;

    public ApplicationContext(DbContextOptions<ApplicationContext> options, IOptions<ApplicationContextConfig> config)
        : base(options)
    {
        _dataSource = CreateDataSource(config.Value.Pg);
    }

    public DbSet<AccountRecovery> AccountRecoveries { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<AccountConfirmation> AccountConfirmations { get; set; } = null!;
    public DbSet<Leaderboard> Leaderboards { get; set; } = null!;
    public DbSet<Run> Runs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

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
        opt.UseNpgsql(_dataSource, o => o.UseNodaTime());
        opt.UseSnakeCaseNamingConvention();
    }

    private static NpgsqlDataSource CreateDataSource(PostgresConfig c)
    {
        return _dataSourceCache.GetOrAdd(c, config =>
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
        });
    }
}
