namespace MusiQL.Api;

public static class RateLimits
{
    public const string Query = "query";
    public const string Auth = "auth";
    public const string Register = "register";
    public const string Write = "write";
}

public sealed class LimitsOptions
{
    public const string Section = "Limits";

    // Per client address across every route.
    public int RequestsPerTenSeconds { get; set; } = 120;

    // Per client address on login, refresh and logout.
    public int AuthPerMinute { get; set; } = 10;

    // Per client address on registration.
    public int RegistrationsPerHour { get; set; } = 5;

    // Per user on playlist and Spotify account mutations.
    public int WritesPerMinute { get; set; } = 30;

    // Per user on query, catalog lookup and export routes.
    public int QueriesPerTenSeconds { get; set; } = 30;

    // Zero means unlimited.
    public int MaxUsers { get; set; } = 0;

    public int MaxPlaylistsPerUser { get; set; } = 200;

    public int MaxActiveRefreshTokensPerUser { get; set; } = 20;

    public int MaxEmailLength { get; set; } = 254;

    public int MaxPasswordLength { get; set; } = 128;

    public long MaxRequestBodyBytes { get; set; } = 64 * 1024;
}
