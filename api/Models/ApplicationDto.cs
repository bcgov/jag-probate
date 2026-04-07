using System;

namespace Probate.Api.Models;

/// <summary>
/// Application (CHEFS submission) returned to the dashboard.
/// </summary>
public class ApplicationDto
{
    public string Id { get; set; } = string.Empty;
    public string ConfirmationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
    public string FormVersionId { get; set; } = string.Empty;
    public bool Deleted { get; set; } = false;
    public bool LateEntry { get; set; } = false;
}
