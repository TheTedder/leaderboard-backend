using System.ComponentModel.DataAnnotations;

namespace LeaderboardBackend.Models.Entities;

public class ApplicationContextConfig
{
    public const string KEY = "ApplicationContext";

    public bool MigrateDb { get; set; } = false;

    [Required]
    public required PostgresConfig Pg { get; set; }
}

public record PostgresConfig
{
    [Required]
    public required string Host { get; set; }

    [Required]
    public required string User { get; set; }

    [Required]
    public required string Password { get; set; }

    [Required]
    public required string Db { get; set; }
    public ushort? Port { get; set; }
}
