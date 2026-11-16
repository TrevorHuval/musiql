using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusiQL.Api.Contracts;
using MusiQL.Data.App;

namespace MusiQL.Api.Spotify;

public sealed class SpotifyAccessRequestService(
    AppDbContext db,
    IAccessRequestMailer mailer,
    IOptions<AccessRequestOptions> options,
    TimeProvider clock,
    ILogger<SpotifyAccessRequestService> log)
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    public async Task<SpotifyAccessRequestStatus> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var existing = await db.SpotifyAccessRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);
        return Status(existing);
    }

    public async Task<SpotifyAccessRequestStatus> RequestAsync(
        Guid userId, string? spotifyEmail, string? note, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Configured)
        {
            throw new AccessRequestsDisabledException();
        }

        var email = NormalizeEmail(spotifyEmail)
            ?? throw new AccessRequestValidationException("Enter the email address of your Spotify account.");
        var cleanNote = NormalizeNote(note);

        if (await db.SpotifyAccounts.AnyAsync(a => a.UserId == userId, ct))
        {
            throw new AccessRequestValidationException("Your Spotify account is already connected.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.SpotifyAccessRequests.FirstOrDefaultAsync(r => r.UserId == userId, ct);
        if (existing is not null && existing.RequestedAt + o.Cooldown > now)
        {
            throw new AccessRequestCooldownException(existing.RequestedAt + o.Cooldown);
        }

        var since = now - Day;
        var recent = await db.SpotifyAccessRequests.CountAsync(r => r.RequestedAt > since, ct);
        if (recent >= o.MaxPerDay)
        {
            log.LogWarning("Spotify access request refused: daily limit of {Limit} reached.", o.MaxPerDay);
            throw new AccessRequestLimitException();
        }

        var accountEmail = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        await mailer.SendAsync(
            "MusiQL: Spotify access request",
            BuildBody(userId, accountEmail, email, cleanNote, now),
            ct);

        if (existing is null)
        {
            existing = new SpotifyAccessRequest { UserId = userId };
            db.SpotifyAccessRequests.Add(existing);
        }

        existing.SpotifyEmail = email;
        existing.Note = cleanNote;
        existing.RequestedAt = now;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Spotify access request sent for user {UserId}.", userId);
        return Status(existing);
    }

    private SpotifyAccessRequestStatus Status(SpotifyAccessRequest? request)
    {
        var o = options.Value;
        return new SpotifyAccessRequestStatus(
            o.Configured,
            request?.RequestedAt,
            request?.SpotifyEmail,
            request is null ? null : request.RequestedAt + o.Cooldown);
    }

    // Accepts a bare address only: no display name, no line breaks, no
    // control characters, within the RFC length limit.
    public static string? NormalizeEmail(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Length > SpotifyAccessRequest.MaxEmailLength
            || trimmed.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '<' or '>' or '"' or ',' or ';'))
        {
            return null;
        }

        if (!MailAddress.TryCreate(trimmed, out var parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.Ordinal)
            || !parsed.Host.Contains('.'))
        {
            return null;
        }

        return trimmed;
    }

    // Keeps line breaks, drops every other control character, caps the length.
    public static string? NormalizeNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (c == '\n' || !char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length > SpotifyAccessRequest.MaxNoteLength)
        {
            throw new AccessRequestValidationException(
                $"Keep the note under {SpotifyAccessRequest.MaxNoteLength} characters.");
        }

        return cleaned.Length == 0 ? null : cleaned;
    }

    // The note is quoted line by line so it cannot pass for one of the fields.
    public static string BuildBody(
        Guid userId, string? accountEmail, string spotifyEmail, string? note, DateTime requestedAt)
    {
        var body = new StringBuilder();
        body.AppendLine("Someone asked to link their Spotify account to MusiQL.");
        body.AppendLine();
        body.AppendLine($"Spotify account email: {spotifyEmail}");
        body.AppendLine($"MusiQL account email: {accountEmail ?? "(none)"}");
        body.AppendLine($"MusiQL user id: {userId}");
        body.AppendLine($"Requested at: {requestedAt:yyyy-MM-dd HH:mm} UTC");
        body.AppendLine();
        if (note is not null)
        {
            body.AppendLine("Their note:");
            foreach (var line in note.Split('\n'))
            {
                body.AppendLine($"> {line}");
            }

            body.AppendLine();
        }

        body.AppendLine("Both addresses were typed by the user and are unverified.");
        body.AppendLine("To approve, add the Spotify email under User Management for the MusiQL app at");
        body.AppendLine("https://developer.spotify.com/dashboard");
        return body.ToString();
    }
}

public sealed class AccessRequestsDisabledException() : Exception("Spotify access requests are not enabled.");

public sealed class AccessRequestValidationException(string message) : Exception(message);

public sealed class AccessRequestCooldownException(DateTime retryAt)
    : Exception("You already asked for access recently.")
{
    public DateTime RetryAt { get; } = retryAt;
}

public sealed class AccessRequestLimitException() : Exception("Too many access requests today.");

public sealed class AccessRequestDeliveryException() : Exception("The access request email could not be sent.");

public sealed class SpotifyAccessRestrictedException()
    : Exception("This Spotify account has not been added to MusiQL's Spotify app.");
