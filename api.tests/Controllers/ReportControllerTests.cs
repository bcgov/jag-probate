using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
using Probate.Api.Controllers;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Tests.Controllers;

public class ReportControllerTests
{
    private readonly Mock<IPDFGenerationService> _pdfService = new();
    private readonly Mock<ITemplateService> _templateService = new();

    [Fact]
    public async Task GenerateFromSubmission_ReturnsGeneratedPdf()
    {
        _templateService
            .Setup(service =>
                service.RenderTemplates("P1", It.IsAny<object>(), It.IsAny<string?>())
            )
            .Returns(["<h1>P1</h1>"]);
        _pdfService
            .Setup(service =>
                service.GeneratePdfAsync(
                    It.IsAny<PdfGenerationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ReportRequestResult<ReportModel>
                {
                    ResultStatus = ReportResultType.Success,
                    ResourcePayload = new ReportModel
                    {
                        Data = Convert.ToBase64String([1, 2, 3]),
                        FileName = "P1.pdf",
                    },
                }
            );
        var controller = new ReportController(_pdfService.Object, _templateService.Object);

        var result = await controller.GenerateFromSubmission(
            new GenerateReportFromSubmissionRequest
            {
                TemplateKey = "P1",
                SubmissionData = new { Name = "Test" },
            },
            CancellationToken.None
        );

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("P1.pdf", file.FileDownloadName);
        Assert.Equal([1, 2, 3], file.FileContents);
        _pdfService.Verify(
            service =>
                service.GeneratePdfAsync(
                    It.Is<PdfGenerationRequest>(request =>
                        request.FileName == "P1.pdf" && request.Documents.Count == 1
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GenerateFromSubmission_EnrichesP2DataBeforeRendering()
    {
        _templateService
            .Setup(service =>
                service.RenderTemplates(
                    "P2",
                    It.Is<JObject>(data =>
                        data["p2SpouseRows"] is JArray
                        && data["p2ChildRows"] is JArray
                        && data["p2SuccessorRows"] is JArray
                    ),
                    It.IsAny<string?>()
                )
            )
            .Returns(["<h1>P2</h1>"]);
        _pdfService
            .Setup(service =>
                service.GeneratePdfAsync(
                    It.IsAny<PdfGenerationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ReportRequestResult<ReportModel>
                {
                    ResultStatus = ReportResultType.Success,
                    ResourcePayload = new ReportModel
                    {
                        Data = Convert.ToBase64String([1]),
                        FileName = "P2.pdf",
                    },
                }
            );
        var controller = new ReportController(_pdfService.Object, _templateService.Object);

        var result = await controller.GenerateFromSubmission(
            new GenerateReportFromSubmissionRequest
            {
                TemplateKey = "P2",
                SubmissionData = new { applicant = new { applicantSuccessorData = new object[0] } },
            },
            CancellationToken.None
        );

        Assert.IsType<FileContentResult>(result);
        _templateService.Verify(
            service =>
                service.RenderTemplates(
                    "P2",
                    It.Is<JObject>(data => data["p2SuccessorRows"] is JArray),
                    It.IsAny<string?>()
                ),
            Times.Once
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("P1/unsafe")]
    public async Task GenerateFromSubmission_RejectsInvalidTemplateKey(string templateKey)
    {
        var controller = new ReportController(_pdfService.Object, _templateService.Object);

        var result = await controller.GenerateFromSubmission(
            new GenerateReportFromSubmissionRequest { TemplateKey = templateKey },
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result);
        _pdfService.Verify(
            service =>
                service.GeneratePdfAsync(
                    It.IsAny<PdfGenerationRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GenerateFromSubmission_ReturnsBadRequestForProviderError()
    {
        _templateService
            .Setup(service =>
                service.RenderTemplates("P1", It.IsAny<object>(), It.IsAny<string?>())
            )
            .Returns([]);
        _pdfService
            .Setup(service =>
                service.GeneratePdfAsync(
                    It.IsAny<PdfGenerationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ReportRequestResult<ReportModel>
                {
                    ResultStatus = ReportResultType.Error,
                    ResultError = new ReportRequestResultError
                    {
                        ReportResultMessage = "Generation failed",
                    },
                }
            );
        var controller = new ReportController(_pdfService.Object, _templateService.Object);

        var result = await controller.GenerateFromSubmission(
            new GenerateReportFromSubmissionRequest { TemplateKey = "P1" },
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
