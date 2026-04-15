using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Probate.Api.Models.CDogs;
using Refit;

namespace Probate.Api.Infrastructure.CDogs;

public interface ICDogsApi
{
    [Post("/template/render")]
    Task<HttpResponseMessage> GenerateDocumentAsync(
        [Body] CDogsRequestModel request,
        CancellationToken ct = default
    );
}
