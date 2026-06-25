using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

/// <summary>
/// API endpoints for retrieving court location information
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CourtLocationsController : ControllerBase
{
    private readonly ICourtLocationService _courtLocationService;

    public CourtLocationsController(ICourtLocationService courtLocationService)
    {
        _courtLocationService = courtLocationService;
    }

    /// <summary>
    /// Gets all available court locations from eFiling Hub
    /// Results are cached for 24 hours to minimize API calls
    /// </summary>
    /// <returns>Court locations response</returns>
    /// <response code="200">Returns the list of court locations</response>
    /// <response code="500">If there was an error fetching court locations</response>
    [HttpGet]
    [ProducesResponseType(typeof(CourtLocationResult), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<CourtLocationResult>> GetCourtLocations()
    {
        var locations = await _courtLocationService.GetCourtLocationsAsync();
        return Ok(locations);
    }
}
