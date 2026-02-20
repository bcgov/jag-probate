using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

/// <summary>
/// Provides current and previous CHEFS applications (submissions) for a form.
/// Form ID is passed in by the caller (e.g. from the frontend) and must match configured Chefs:FormId.
/// </summary>
public interface IChefsApplicationService
{
    /// <summary>
    /// Gets all applications (submissions) for the given form ID.
    /// Throws <see cref="InvalidOperationException"/> if formId is not allowed or Chefs:FormId is not configured.
    /// </summary>
    Task<IReadOnlyList<ApplicationDto>> GetApplicationsAsync(
        string formId,
        CancellationToken cancellationToken = default
    );
}
