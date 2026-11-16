namespace MusiQL.Api.Spotify;

// While the Spotify app is in Development Mode only allowlisted accounts can
// link. Users ask to be added through MusiQL, which emails the operator. Every
// value comes from configuration so no address or credential lives in the repo.
public sealed class AccessRequestOptions
{
    public const string Section = "AccessRequests";

    // Where requests are delivered. Blank turns the feature off.
    public string NotifyEmail { get; set; } = "";

    // A verified sender identity on the SMTP service.
    public string FromEmail { get; set; } = "";

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";

    // How long one user waits before asking again.
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromHours(24);

    // Ceiling on emails across all users in a rolling 24 hours.
    public int MaxPerDay { get; set; } = 10;

    public bool Configured =>
        !string.IsNullOrWhiteSpace(NotifyEmail)
        && !string.IsNullOrWhiteSpace(FromEmail)
        && !string.IsNullOrWhiteSpace(SmtpHost);
}
