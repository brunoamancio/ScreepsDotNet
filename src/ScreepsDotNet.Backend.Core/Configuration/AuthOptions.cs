namespace ScreepsDotNet.Backend.Core.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool UseNativeAuth { get; set; }

    public int TokenTtlSeconds { get; set; } = 60;

    public IList<AuthTicketOptions> Tickets { get; set; } = [];
}

public sealed class AuthTicketOptions
{
    public string Ticket { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string SteamId { get; set; } = string.Empty;
}
