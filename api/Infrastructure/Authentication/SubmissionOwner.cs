using System;
using System.Security.Claims;

namespace Probate.Api.Infrastructure.Authentication;

/// <summary>
/// The authoritative Keycloak user ID and optional deprecated username fallback.
/// Username fallback is used only for rows whose CreatedByUserId is null.
/// </summary>
public sealed record SubmissionOwner
{
    public string UserId { get; }
    public string? LegacyUsername { get; }

    /// <summary>
    /// Requires a user ID; preserves exact claim values without case folding or trimming.
    /// The legacy username is optional and cannot authorize an owner-ID row.
    /// </summary>
    public SubmissionOwner(string ownerUserId, string? legacyUsername = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        UserId = ownerUserId;
        LegacyUsername = string.IsNullOrWhiteSpace(legacyUsername) ? null : legacyUsername;
    }

    /// <summary>
    /// Reads sub, falling back to ASP.NET's mapped NameIdentifier claim.
    /// A username alone never identifies an owner. The legacy username fallback will be removed
    /// after legacy rows have been claimed and CreatedBy has been retired.
    /// </summary>
    public static SubmissionOwner? FromPrincipal(ClaimsPrincipal principal)
    {
        var ownerUserId = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(ownerUserId))
            ownerUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(ownerUserId)
            ? null
            : new SubmissionOwner(ownerUserId, principal.FindFirstValue("preferred_username"));
    }
}
