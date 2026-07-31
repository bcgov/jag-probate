using System.Collections.Generic;

namespace Probate.Api.Models;

/// <summary>
/// Sidebar substep (panel) metadata. Never includes FormId/ApiKey.
/// </summary>
public class SidebarSubstepDto
{
    public string Key { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Disabled { get; set; }
}

/// <summary>
/// Sidebar step metadata for a single top-level CHEFS form. Never includes
/// FormId/ApiKey - those remain server-side only.
/// </summary>
public class SidebarStepDto
{
    public string Key { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public IReadOnlyList<SidebarSubstepDto> Children { get; set; } = new List<SidebarSubstepDto>();
}
