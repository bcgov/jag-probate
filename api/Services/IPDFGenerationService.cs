using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models;

namespace Probate.Api.Services;

public interface IPDFGenerationService
{
    Task<ReportRequestResult<ReportModel>> GeneratePdfAsync(
        PdfGenerationRequest request,
        CancellationToken ct = default
    );
}
