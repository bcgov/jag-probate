using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;
using Probate.Api.Models.CDogs;

namespace Probate.Api.Infrastructure.CDogs;

public interface ICDogsDelegate
{
    Task<ReportRequestResult<ReportModel>> GenerateReportAsync(
        CDogsRequestModel request,
        CancellationToken ct = default
    );
}
