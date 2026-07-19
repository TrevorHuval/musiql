namespace MusiQL.Api.Contracts;

public sealed record PlaylistRequest(string Name, string? Description, string Mql);

public sealed record PlaylistResponse(
    Guid Id,
    string Name,
    string? Description,
    string Mql,
    DateTime CreatedAt,
    DateTime UpdatedAt);
