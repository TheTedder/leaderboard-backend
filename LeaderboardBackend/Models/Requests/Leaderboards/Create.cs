namespace LeaderboardBackend.Models.Requests.Leaderboards;

public record CreateLeaderboardRequest
{
	public string Name { get; set; } = null!;
	public string Slug { get; set; } = null!;
}