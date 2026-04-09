using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Infrastructure.CDogs;
using Probate.Api.Models;
using Probate.Api.Models.CDogs;

namespace Probate.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ReportController : ControllerBase
{
    private readonly ICDogsDelegate cdogsDelegate;

    public ReportController(ICDogsDelegate cdogsDelegate) => this.cdogsDelegate = cdogsDelegate;

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] CDogsRequestModel request,
        CancellationToken ct
    )
    {
        var result = await this.cdogsDelegate.GenerateReportAsync(request, ct);

        if (result.ResultStatus == ReportResultType.Error)
            return BadRequest(result.ResultError);

        return Ok(result.ResourcePayload);
    }
}
