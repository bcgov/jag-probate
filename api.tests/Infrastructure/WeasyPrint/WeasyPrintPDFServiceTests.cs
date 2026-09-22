using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Probate.Api.Infrastructure.WeasyPrint;
using Probate.Api.Models;

namespace Probate.Api.Tests.Infrastructure.WeasyPrint;

public class WeasyPrintPDFServiceTests
{
    [Fact]
    public async Task GeneratePdfAsync_ReturnsEncodedPdfAndDisposesResponse()
    {
        var content = new TrackingContent("pdf bytes");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        var api = new Mock<IWeasyPrintApi>();
        api.Setup(value =>
                value.GeneratePdfAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);
        var service = new WeasyPrintPDFService(
            api.Object,
            NullLogger<WeasyPrintPDFService>.Instance
        );

        var result = await service.GeneratePdfAsync(
            new PdfGenerationRequest { Documents = ["<p>Document</p>"], FileName = "report.pdf" }
        );

        Assert.Equal(ReportResultType.Success, result.ResultStatus);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("pdf bytes")),
            result.ResourcePayload!.Data
        );
        Assert.Equal("report.pdf", result.ResourcePayload.FileName);
        Assert.True(content.IsDisposed);
    }

    [Fact]
    public async Task GeneratePdfAsync_ReturnsProviderErrorAndDisposesResponse()
    {
        var content = new TrackingContent("Bad request");
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = content,
        };
        var api = new Mock<IWeasyPrintApi>();
        api.Setup(value =>
                value.GeneratePdfAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);
        var service = new WeasyPrintPDFService(
            api.Object,
            NullLogger<WeasyPrintPDFService>.Instance
        );

        var result = await service.GeneratePdfAsync(
            new PdfGenerationRequest { FileName = "report.pdf" }
        );

        Assert.Equal(ReportResultType.Error, result.ResultStatus);
        Assert.Equal(
            "WeasyPrint returned HTTP 400: Bad request",
            result.ResultError!.ReportResultMessage
        );
        Assert.True(content.IsDisposed);
    }

    private sealed class TrackingContent(string value) : StringContent(value)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
