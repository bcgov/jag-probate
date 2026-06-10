using System.Collections.Generic;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

/// <summary>
/// Service for retrieving court location information from eFiling Hub
/// </summary>
public interface ICourtLocationService
{
    /// <summary>
    /// Gets all available court locations from eFiling Hub API
    /// Results are cached to minimize API calls
    /// </summary>
    /// <returns>Court locations response</returns>
    Task<CourtLocationResult> GetCourtLocationsAsync();
}
