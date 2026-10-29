using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

public interface IChefsApplicationService
{
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
