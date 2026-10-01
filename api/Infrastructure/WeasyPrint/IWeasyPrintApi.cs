using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Probate.Api.Infrastructure.WeasyPrint;

public interface IWeasyPrintApi
{
    [Post("/multiple")]
    Task<HttpResponseMessage> GeneratePdfAsync(
        [Body] IReadOnlyList<string> documents,
        [Query] string filename,
        CancellationToken ct = default
    );
}
