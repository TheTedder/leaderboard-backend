using System.ComponentModel.DataAnnotations;
using LeaderboardBackend.Models.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using NpgsqlTypes;

namespace LeaderboardBackend.Models.Entities;

/// <summary>
///     Represents a collection of `Category` entities.
/// </summary>
public class Leaderboard : IHasUpdateTimestamp, IHasDeletionTimestamp
{
    /// <summary>
    ///     The unique identifier of the `Leaderboard`.<br/>
    ///     Generated on creation.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     The display name of the `Leaderboard`.
    /// </summary>
    /// <example>Foo Bar</example>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    ///     The search vector for this Leaderboard. This property is automatically computed
    ///     from the <see cref="Name" /> and <see cref="Slug" />.
    /// </summary>
#nullable disable warnings
    public NpgsqlTsVector SearchVector { get; set; }
#nullable restore warnings

    /// <summary>
    ///     The URL-scoped unique identifier of the `Leaderboard`.<br/>
    ///     Must be [2, 80] in length and consist only of alphanumeric characters and hyphens.
    /// </summary>
    /// <example>foo-bar</example>
    [StringLength(80, MinimumLength = 2)]
    [RegularExpression(SlugRule.REGEX)]
    public required string Slug { get; set; }

    /// <summary>
    ///     The general information for the Leaderboard.
    /// </summary>
    /// <example>Timer starts on selecting New Game and ends when the final boss is beaten.</example>
    public string Info { get; set; } = null!;

    /// <summary>
    ///     The time the Leaderboard was created.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    ///     The last time the Leaderboard was updated or <see langword="null" />.
    /// </summary>
    public Instant? UpdatedAt { get; set; }

    /// <summary>
    ///     The time at which the Leaderboard was deleted, or <see langword="null" /> if the Leaderboard has not been deleted.
    /// </summary>
    public Instant? DeletedAt { get; set; }

    /// <summary>
    ///     A collection of `Category` entities for the `Leaderboard`.
    /// </summary>
    public List<Category>? Categories { get; set; }
}

public static class LeaderboardExtensions
{
    /// <summary>
    ///     Search for a leaderboard whose name or slug matches the specified query using
    ///     <see href="https://www.postgresql.org/docs/current/textsearch-controls.html#TEXTSEARCH-PARSING-QUERIES">web search syntax</see>.
    /// </summary>
    public static IQueryable<Leaderboard> Search(this IQueryable<Leaderboard> lbSource, string query) =>
        lbSource.Where(lb => lb.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(query)));
}

public class LeaderboardEntityTypeConfig : IEntityTypeConfiguration<Leaderboard>
{
    public void Configure(EntityTypeBuilder<Leaderboard> builder)
    {
        builder.HasIndex(l => l.Slug)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.Property(l => l.Info)
            .HasDefaultValue("");

        builder.HasGeneratedTsVectorColumn(
            lb => lb.SearchVector,
            "english",
            lb => new { lb.Name, lb.Slug }
        ).HasIndex(lb => lb.SearchVector)
            .HasMethod("GIN");
    }
}
