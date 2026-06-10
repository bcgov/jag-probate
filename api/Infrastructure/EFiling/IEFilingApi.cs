using System.Threading.Tasks;
using Probate.Api.Models.EFiling;
using Refit;

namespace Probate.Api.Infrastructure.EFiling;

/// <summary>
/// Refit interface for eFiling Hub API endpoints
/// </summary>
public interface IEFilingApi
{
    /// <summary>
    /// Gets court locations from eFiling Hub
    /// </summary>
    /// <param name="courtLevel">Court level code</param>
    /// <returns>List of court locations</returns>
    [Get("/courts")]
    Task<EFilingCourtsResponse> GetCourtsAsync([Query] string courtLevel);
}
