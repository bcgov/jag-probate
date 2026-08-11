using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Probate.Api.Controllers;
using Probate.Api.Models;
using Probate.Api.Services;
using Xunit;

namespace Probate.Api.Tests.Controllers;

public class SubmissionsControllerTests
{
    private readonly Mock<ISubmissionService> _mockService;
    private readonly Mock<IStepDataService> _mockStepDataService;
    private readonly Mock<ILogger<SubmissionsController>> _mockLogger;
    private readonly SubmissionsController _controller;

    public SubmissionsControllerTests()
    {
        _mockService = new Mock<ISubmissionService>();
        _mockStepDataService = new Mock<IStepDataService>();
        _mockLogger = new Mock<ILogger<SubmissionsController>>();
        _controller = new SubmissionsController(
            _mockService.Object,
            _mockStepDataService.Object,
            _mockLogger.Object
        );
    }

    /// Sets up the controller's User principal with the given preferred_username claim.
    private void SetUser(string? username)
    {
        var claims = username is not null
            ? new[] { new Claim("preferred_username", username) }
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }

    // ── POST /api/Submissions ─────────────────────────────────────────────

    [Fact]
    public async Task CreateSubmission_WithValidDto_Returns201WithLocation()
    {
        // Arrange
        var dto = new CreateSubmissionDto
        {
            ChefsSubmissionId = "chefs-new-001",
            CreatedBy = "user1",
            Status = "draft",
        };
        var created = new SubmissionResponseDto
        {
            PublicId = Guid.NewGuid(),
            ChefsSubmissionId = "chefs-new-001",
            CreatedBy = "user1",
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
        };

        _mockService
            .Setup(x => x.CreateSubmissionAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.CreateSubmission(dto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var returned = Assert.IsType<SubmissionResponseDto>(createdResult.Value);
        Assert.NotEqual(Guid.Empty, returned.PublicId);
        Assert.Equal("chefs-new-001", returned.ChefsSubmissionId);
    }

    [Fact]
    public async Task CreateSubmission_WithEmptyChefsSubmissionId_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateSubmissionDto
        {
            ChefsSubmissionId = "",
            CreatedBy = "user1",
            Status = "draft",
        };

        // Act
        var result = await _controller.CreateSubmission(dto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var message = badRequest
            .Value?.GetType()
            .GetProperty("message")
            ?.GetValue(badRequest.Value)
            ?.ToString();
        Assert.Equal("ChefsSubmissionId is required.", message);
    }

    [Fact]
    public async Task CreateSubmission_WithWhitespaceChefsSubmissionId_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateSubmissionDto
        {
            ChefsSubmissionId = "   ",
            CreatedBy = "user1",
            Status = "draft",
        };

        // Act
        var result = await _controller.CreateSubmission(dto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task CreateSubmission_DoesNotCallService_WhenValidationFails()
    {
        // Arrange
        var dto = new CreateSubmissionDto
        {
            ChefsSubmissionId = "",
            CreatedBy = "user1",
            Status = "draft",
        };

        // Act
        await _controller.CreateSubmission(dto);

        // Assert
        _mockService.Verify(
            x =>
                x.CreateSubmissionAsync(
                    It.IsAny<CreateSubmissionDto>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── GET /api/Submissions/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GetSubmission_WhenFound_ReturnsOkWithDto()
    {
        // Arrange
        var submissionId = Guid.NewGuid();
        var expected = new SubmissionResponseDto
        {
            PublicId = submissionId,
            ChefsSubmissionId = "chefs-abc-123",
            ApplicantName = "Jane Doe",
            CreatedBy = "jdoe",
            Status = "draft",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        _mockService
            .Setup(x => x.GetSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetSubmission(submissionId);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<SubmissionResponseDto>(okResult.Value);
        Assert.Equal(submissionId, dto.PublicId);
        Assert.Equal("chefs-abc-123", dto.ChefsSubmissionId);
        Assert.Equal("Jane Doe", dto.ApplicantName);
        Assert.Equal("draft", dto.Status);
    }

    [Fact]
    public async Task GetSubmission_WhenNotFound_ReturnsNotFoundWithMessage()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        _mockService
            .Setup(x => x.GetSubmissionByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubmissionResponseDto?)null);

        // Act
        var result = await _controller.GetSubmission(missingId);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var message = notFound
            .Value?.GetType()
            .GetProperty("message")
            ?.GetValue(notFound.Value)
            ?.ToString();
        Assert.Equal($"Submission {missingId} not found.", message);
    }

    [Fact]
    public async Task GetSubmission_PassesCancellationTokenToService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var submissionId = Guid.NewGuid();
        _mockService
            .Setup(x => x.GetSubmissionByIdAsync(submissionId, cts.Token))
            .ReturnsAsync(new SubmissionResponseDto { PublicId = submissionId });

        // Act
        await _controller.GetSubmission(submissionId, cts.Token);

        // Assert
        _mockService.Verify(x => x.GetSubmissionByIdAsync(submissionId, cts.Token), Times.Once);
    }

    // ── GET /api/Submissions ──────────────────────────────────────────────

    [Fact]
    public async Task GetSubmissions_WithAuthenticatedUser_ReturnsOkWithList()
    {
        // Arrange
        SetUser("jdoe");
        var submissions = new List<SubmissionResponseDto>
        {
            new()
            {
                PublicId = Guid.NewGuid(),
                ChefsSubmissionId = "chefs-001",
                CreatedBy = "jdoe",
                Status = "draft",
            },
            new()
            {
                PublicId = Guid.NewGuid(),
                ChefsSubmissionId = "chefs-002",
                CreatedBy = "jdoe",
                Status = "submitted",
            },
        };

        _mockService
            .Setup(x => x.GetSubmissionsByUserAsync("jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(submissions);

        // Act
        var result = await _controller.GetSubmissions();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IReadOnlyList<SubmissionResponseDto>>>(
            result
        );
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<List<SubmissionResponseDto>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task GetSubmissions_WithNoUsernameClaim_ReturnsUnauthorized()
    {
        // Arrange — authenticated but no preferred_username claim
        SetUser(null);

        // Act
        var result = await _controller.GetSubmissions();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IReadOnlyList<SubmissionResponseDto>>>(
            result
        );
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
        var message = unauthorized
            .Value?.GetType()
            .GetProperty("message")
            ?.GetValue(unauthorized.Value)
            ?.ToString();
        Assert.Equal("Unable to identify current user.", message);
    }

    [Fact]
    public async Task GetSubmissions_WithAuthenticatedUser_PassesUsernameToService()
    {
        // Arrange
        SetUser("alice");
        _mockService
            .Setup(x => x.GetSubmissionsByUserAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubmissionResponseDto>());

        // Act
        await _controller.GetSubmissions();

        // Assert
        _mockService.Verify(
            x => x.GetSubmissionsByUserAsync("alice", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetSubmissions_WhenUserHasNoSubmissions_ReturnsEmptyList()
    {
        // Arrange
        SetUser("newuser");
        _mockService
            .Setup(x => x.GetSubmissionsByUserAsync("newuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubmissionResponseDto>());

        // Act
        var result = await _controller.GetSubmissions();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IReadOnlyList<SubmissionResponseDto>>>(
            result
        );
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<List<SubmissionResponseDto>>(okResult.Value);
        Assert.Empty(returned);
    }

    // ── POST /api/Submissions/upsert ──────────────────────────────────────

    [Fact]
    public async Task UpsertSubmission_WithValidDto_ReturnsOkWithResult()
    {
        // Arrange
        var dto = new CreateSubmissionDto { ChefsSubmissionId = "chefs-upd-001", Status = "draft" };
        var upserted = new SubmissionResponseDto
        {
            PublicId = Guid.NewGuid(),
            ChefsSubmissionId = "chefs-upd-001",
            Status = "draft",
        };

        _mockService
            .Setup(x => x.UpsertSubmissionAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upserted);

        // Act
        var result = await _controller.UpsertSubmission(dto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var returned = Assert.IsType<SubmissionResponseDto>(okResult.Value);
        Assert.NotEqual(Guid.Empty, returned.PublicId);
        Assert.Equal("chefs-upd-001", returned.ChefsSubmissionId);
    }

    [Fact]
    public async Task UpsertSubmission_WithEmptyChefsSubmissionId_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateSubmissionDto { ChefsSubmissionId = "", Status = "draft" };

        // Act
        var result = await _controller.UpsertSubmission(dto);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var message = badRequest
            .Value?.GetType()
            .GetProperty("message")
            ?.GetValue(badRequest.Value)
            ?.ToString();
        Assert.Equal("ChefsSubmissionId is required.", message);
    }

    [Fact]
    public async Task UpsertSubmission_DoesNotCallService_WhenValidationFails()
    {
        // Arrange
        var dto = new CreateSubmissionDto { ChefsSubmissionId = "  " };

        // Act
        await _controller.UpsertSubmission(dto);

        // Assert
        _mockService.Verify(
            x =>
                x.UpsertSubmissionAsync(
                    It.IsAny<CreateSubmissionDto>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // ── DELETE /api/Submissions/{id} ──────────────────────────────────────

    [Fact]
    public async Task DeleteSubmission_WhenSuccessful_ReturnsNoContent()
    {
        // Arrange
        var deleteId = Guid.NewGuid();
        _mockService
            .Setup(x => x.DeleteSubmissionAsync(deleteId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteSubmission(deleteId);

        // Assert
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
    }

    [Fact]
    public async Task DeleteSubmission_CallsServiceWithCorrectId()
    {
        // Arrange
        var deleteId = Guid.NewGuid();
        _mockService
            .Setup(x => x.DeleteSubmissionAsync(deleteId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.DeleteSubmission(deleteId);

        // Assert
        _mockService.Verify(
            x => x.DeleteSubmissionAsync(deleteId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteSubmission_PassesCancellationTokenToService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var deleteId = Guid.NewGuid();
        _mockService
            .Setup(x => x.DeleteSubmissionAsync(deleteId, cts.Token))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.DeleteSubmission(deleteId, cts.Token);

        // Assert
        _mockService.Verify(x => x.DeleteSubmissionAsync(deleteId, cts.Token), Times.Once);
    }

    // ── POST /api/Submissions/{id}/submit ────────────────────────────────

    [Fact]
    public async Task SubmitApplication_CompilesAndFinalizes_ReturnsOk()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        var compiledData = "{\"name\":\"test\",\"age\":30}";
        var finalized = new SubmissionResponseDto
        {
            PublicId = publicId,
            ChefsSubmissionId = "chefs-final",
            ApplicantName = "Jane Doe",
            CreatedBy = "jdoe",
            Status = "submitted",
            CreatedAt = DateTime.UtcNow,
        };

        _mockStepDataService
            .Setup(x => x.GetCompiledDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledData);
        _mockService
            .Setup(x => x.FinalizeSubmissionAsync(publicId, compiledData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalized);

        // Act
        var result = await _controller.SubmitApplication(publicId);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returned = Assert.IsType<SubmissionResponseDto>(okResult.Value);
        Assert.Equal("submitted", returned.Status);
        Assert.Equal(publicId, returned.PublicId);
    }

    [Fact]
    public async Task SubmitApplication_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var publicId = Guid.NewGuid();

        _mockStepDataService
            .Setup(x => x.GetCompiledDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.SubmitApplication(publicId);

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task SubmitApplication_PassesCompiledDataToFinalize()
    {
        // Arrange
        var publicId = Guid.NewGuid();
        var compiledData = "{\"step1\":\"data\",\"step3\":\"data\"}";

        _mockStepDataService
            .Setup(x => x.GetCompiledDataAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledData);
        _mockService
            .Setup(x => x.FinalizeSubmissionAsync(publicId, compiledData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionResponseDto
            {
                PublicId = publicId,
                ChefsSubmissionId = "chefs-final",
                ApplicantName = "Jane Doe",
                CreatedBy = "jdoe",
                Status = "submitted",
            });

        // Act
        await _controller.SubmitApplication(publicId);

        // Assert
        _mockService.Verify(
            x => x.FinalizeSubmissionAsync(publicId, compiledData, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── POST /api/Submissions/draft ──────────────────────────────────────

    [Fact]
    public async Task CreateDraftSubmission_WithAuthenticatedUser_Returns201()
    {
        // Arrange
        SetUser("jdoe");
        var created = new SubmissionResponseDto
        {
            PublicId = Guid.NewGuid(),
            ChefsSubmissionId = "",
            ApplicantName = "",
            CreatedBy = "jdoe",
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
        };

        _mockService
            .Setup(x => x.CreateDraftSubmissionAsync("jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.CreateDraftSubmission();

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var returned = Assert.IsType<SubmissionResponseDto>(createdResult.Value);
        Assert.Equal("draft", returned.Status);
        Assert.Equal("jdoe", returned.CreatedBy);
    }

    [Fact]
    public async Task CreateDraftSubmission_WithNoUsernameClaim_ReturnsUnauthorized()
    {
        // Arrange
        SetUser(null);

        // Act
        var result = await _controller.CreateDraftSubmission();

        // Assert
        var actionResult = Assert.IsType<ActionResult<SubmissionResponseDto>>(result);
        Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
    }
}
