using System;

namespace Probate.Api.Models;

/// <summary>
/// Application (CHEFS submission) returned to the dashboard.
/// </summary>
public class ApplicationDto
{
    public string Id { get; set; } = string.Empty;
    public string FormId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
