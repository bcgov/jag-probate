using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Probate.Api.Controllers;
using Probate.Api.Models;
using Probate.Api.Services;
using Xunit;

namespace Probate.Api.Tests.Controllers;

public class StepDataControllerTests
{
    private readonly Mock<IStepDataService> _mockService;
    private readonly StepDataController _controller;

    public StepDataControllerTests()
    {
        _mockService = new Mock<IStepDataService>();
        _controller = new StepDataController(_mockService.Object);
    }

    // ── PUT /api/submissions/{publicId}/steps/{formId} ───────────────────

    [Fact]
    public async Task UpsertStepData_WhenSubmissionExists_ReturnsOk()
    {
        var publicId = Guid.NewGuid();
        var dto = new UpsertStepDataDto
        {
            FormId = "step1",
            Data = "{\"name\":\"test\"}",
            FormVersion = "1.0",
        };
        var expected = new StepDataResponseDto
        {
            PublicId = Guid.NewGuid(),
            FormId = "step1",
            Data = "{\"name\":\"test\"}",
            FormVersion = "1.0",
            CreatedAt = DateTime.UtcNow,
        };

        _mockService
            .Setup(x =>
                x.UpsertStepDataAsync(publicId, It.IsAny<UpsertStepDataDto>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expected);

        var result = await _controller.UpsertStepData(publicId, "step1", dto);

        var actionResult = Assert.IsType<ActionResult<StepDataResponseDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<StepDataResponseDto>(okResult.Value);
        Assert.Equal("step1", returned.FormId);
        Assert.Equal("{\"name\":\"test\"}", returned.Data);
    }

    [Fact]
    public async Task UpsertStepData_SetsFormIdFromRoute()
    {
        var publicId = Guid.NewGuid();
        var dto = new UpsertStepDataDto
        {
            FormId = "will-be-overwritten",
            Data = "{}",
        };

        _mockService
            .Setup(x =>
                x.UpsertStepDataAsync(
                    publicId,
                    It.Is<UpsertStepDataDto>(d => d.FormId == "step3"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new StepDataResponseDto { FormId = "step3" });

        await _controller.UpsertStepData(publicId, "step3", dto);

        _mockService.Verify(
            x =>
                x.UpsertStepDataAsync(
                    publicId,
                    It.Is<UpsertStepDataDto>(d => d.FormId == "step3"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpsertStepData_WhenSubmissionNotFound_ReturnsNotFound()
    {
        var publicId = Guid.NewGuid();
        var dto = new UpsertStepDataDto { FormId = "step1", Data = "{}" };

        _mockService
            .Setup(x =>
                x.UpsertStepDataAsync(publicId, It.IsAny<UpsertStepDataDto>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.UpsertStepData(publicId, "step1", dto);

        var actionResult = Assert.IsType<ActionResult<StepDataResponseDto>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    // ── GET /api/submissions/{publicId}/steps/{formId} ───────────────────

    [Fact]
    public async Task GetStepData_WhenFound_ReturnsOk()
    {
        var publicId = Guid.NewGuid();
        var expected = new StepDataResponseDto
        {
            PublicId = Guid.NewGuid(),
            FormId = "step1",
            Data = "{\"field\":\"value\"}",
        };

        _mockService
            .Setup(x => x.GetStepDataAsync(publicId, "step1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetStepData(publicId, "step1");

        var actionResult = Assert.IsType<ActionResult<StepDataResponseDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<StepDataResponseDto>(okResult.Value);
        Assert.Equal("step1", returned.FormId);
    }

    [Fact]
    public async Task GetStepData_WhenNotFound_ReturnsNotFound()
    {
        var publicId = Guid.NewGuid();

        _mockService
            .Setup(x => x.GetStepDataAsync(publicId, "step99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StepDataResponseDto?)null);

        var result = await _controller.GetStepData(publicId, "step99");

        var actionResult = Assert.IsType<ActionResult<StepDataResponseDto>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    // ── GET /api/submissions/{publicId}/steps ────────────────────────────

    [Fact]
    public async Task GetAllStepData_ReturnsOkWithList()
    {
        var publicId = Guid.NewGuid();
        var steps = new List<StepDataResponseDto>
        {
            new() { FormId = "step1", Data = "{}" },
            new() { FormId = "step3", Data = "{}" },
        };

        _mockService
            .Setup(x => x.GetAllStepDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steps);

        var result = await _controller.GetAllStepData(publicId);

        var actionResult = Assert.IsType<ActionResult<IReadOnlyList<StepDataResponseDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<List<StepDataResponseDto>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetAllStepData_WhenNoSteps_ReturnsEmptyList()
    {
        var publicId = Guid.NewGuid();

        _mockService
            .Setup(x => x.GetAllStepDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StepDataResponseDto>());

        var result = await _controller.GetAllStepData(publicId);

        var actionResult = Assert.IsType<ActionResult<IReadOnlyList<StepDataResponseDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<StepDataResponseDto[]>(okResult.Value);
        Assert.Empty(returned);
    }

    // ── GET /api/submissions/{publicId}/steps/compiled ───────────────────

    [Fact]
    public async Task GetCompiledData_WhenFound_ReturnsJsonContent()
    {
        var publicId = Guid.NewGuid();
        var compiled = "{\"name\":\"test\",\"age\":30}";

        _mockService
            .Setup(x => x.GetCompiledDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiled);

        var result = await _controller.GetCompiledData(publicId);

        var actionResult = Assert.IsType<ActionResult<string>>(result);
        var contentResult = Assert.IsType<ContentResult>(actionResult.Result);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Equal(compiled, contentResult.Content);
    }

    [Fact]
    public async Task GetCompiledData_WhenNotFound_ReturnsNotFound()
    {
        var publicId = Guid.NewGuid();

        _mockService
            .Setup(x => x.GetCompiledDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.GetCompiledData(publicId);

        var actionResult = Assert.IsType<ActionResult<string>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }
}
