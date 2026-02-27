using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

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

    /// <summary>
    /// Gets a short-lived auth token for the CHEFS web component.
    /// </summary>
    Task<string> GetAuthTokenAsync(string formKey, CancellationToken cancellationToken = default);
}
