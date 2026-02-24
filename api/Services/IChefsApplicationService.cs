using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

/// <summary>
/// Provides current and previous CHEFS applications (submissions) for a form.
/// Callers pass a logical form key; the implementation resolves it to the actual CHEFS form GUID via config.
/// </summary>
public interface IChefsApplicationService
{
    /// <summary>
    /// Gets all applications (submissions) for the form identified by <paramref name="formKey"/>.
    /// Throws <see cref="InvalidOperationException"/> if <paramref name="formKey"/> is not present in Chefs:Forms config.
    /// </summary>
    Task<IReadOnlyList<ApplicationDto>> GetApplicationsAsync(
        string formKey,
        CancellationToken cancellationToken = default
    );
}
