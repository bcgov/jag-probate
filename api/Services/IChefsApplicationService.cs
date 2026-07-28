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
    /// Gets a short-lived authentication token (JWT) for embedding CHEFS forms via the web component.
    /// Tokens are typically valid for 15 minutes and should be used immediately for form embedding.
    /// </summary>
    Task<ChefsAuthTokenDto> GetAuthTokenAsync(
        string formKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the application sidebar navigation structure (step/substep title, icon, order) driven
    /// entirely by Chefs:Forms configuration.
    /// </summary>
    IReadOnlyList<SidebarStepDto> GetSidebarStructure();
}
