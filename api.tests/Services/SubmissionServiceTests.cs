using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Options;
using Probate.Api.Services;
using Probate.Db.Models;
using Refit;

namespace Probate.Api.Tests.Services;

public class SubmissionServiceTests
{
    [Fact]
    public async Task FinalizeSubmissionAsync_PersistsChefsIds_AndSkipsCreateOnRetry()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission(
            new StepData { FormId = "form-step-one", Data = "{\"value\":1}" }
        );
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var chefsApi = new Mock<IChefsApi>();
        chefsApi
            .Setup(api =>
                api.CreateSubmissionAsync(
                    "chefs-form-guid",
                    It.IsAny<ChefsCreateSubmissionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChefsCreateSubmissionResponse { Id = "chefs-submission-id" });

        var service = CreateService(db, chefsApi);

        await service.FinalizeSubmissionAsync(submission.PublicId, "{}");
        await service.FinalizeSubmissionAsync(submission.PublicId, "{}");

        var step = await db.StepData.SingleAsync();
        Assert.Equal("chefs-form-guid", step.ChefsFormId);
        Assert.Equal("chefs-submission-id", step.ChefsSubmissionId);
        chefsApi.Verify(
            api =>
                api.CreateSubmissionAsync(
                    "chefs-form-guid",
                    It.IsAny<ChefsCreateSubmissionRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteSubmissionAsync_DeletesEachStep_AndTreatsNotFoundAsSuccess()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission(
            new StepData
            {
                FormId = "form-step-one",
                ChefsFormId = "chefs-form-one",
                ChefsSubmissionId = "chefs-submission-one",
            },
            new StepData
            {
                FormId = "form-step-two",
                ChefsFormId = "chefs-form-two",
                ChefsSubmissionId = "chefs-submission-two",
            }
        );
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var chefsApi = new Mock<IChefsApi>();
        chefsApi
            .Setup(api =>
                api.DeleteSubmissionAsync(
                    "chefs-form-one",
                    "chefs-submission-one",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        chefsApi
            .Setup(api =>
                api.DeleteSubmissionAsync(
                    "chefs-form-two",
                    "chefs-submission-two",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(await CreateApiException(HttpStatusCode.NotFound));

        var service = CreateService(db, chefsApi);

        await service.DeleteSubmissionAsync(submission.PublicId);

        Assert.All(
            await db.StepData.ToListAsync(),
            step =>
            {
                Assert.Null(step.ChefsFormId);
                Assert.Null(step.ChefsSubmissionId);
            }
        );
        chefsApi.Verify(
            api =>
                api.DeleteSubmissionAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task DeleteSubmissionAsync_FailedDeleteRemainsRetryable()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission(
            new StepData
            {
                FormId = "form-step-one",
                ChefsFormId = "chefs-form-guid",
                ChefsSubmissionId = "chefs-submission-id",
            }
        );
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var chefsApi = new Mock<IChefsApi>();
        chefsApi
            .SetupSequence(api =>
                api.DeleteSubmissionAsync(
                    "chefs-form-guid",
                    "chefs-submission-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(await CreateApiException(HttpStatusCode.InternalServerError))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, chefsApi);

        await Assert.ThrowsAsync<ChefsApiException>(() =>
            service.DeleteSubmissionAsync(submission.PublicId)
        );

        var step = await db.StepData.SingleAsync();
        Assert.Equal("chefs-form-guid", step.ChefsFormId);
        Assert.Equal("chefs-submission-id", step.ChefsSubmissionId);

        await service.DeleteSubmissionAsync(submission.PublicId);

        Assert.Null(step.ChefsFormId);
        Assert.Null(step.ChefsSubmissionId);
        chefsApi.Verify(
            api =>
                api.DeleteSubmissionAsync(
                    "chefs-form-guid",
                    "chefs-submission-id",
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    private static ProbateDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProbateDbContext(options);
    }

    private static SubmissionService CreateService(ProbateDbContext db, Mock<IChefsApi> chefsApi)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new ChefsOptions
            {
                Forms = { ["form-step-one"] = new ChefsFormOptions { FormId = "chefs-form-guid" } },
            }
        );

        return new SubmissionService(
            db,
            NullLogger<SubmissionService>.Instance,
            chefsApi.Object,
            options
        );
    }

    private static Submission CreateSubmission(params StepData[] steps)
    {
        return new Submission
        {
            PublicId = Guid.NewGuid(),
            ChefsSubmissionId = string.Empty,
            ApplicantName = string.Empty,
            CreatedBy = "test-user",
            Status = "draft",
            StepDataEntries = steps,
        };
    }

    private static Task<ApiException> CreateApiException(HttpStatusCode statusCode)
    {
        return ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Delete,
            new HttpResponseMessage(statusCode),
            new RefitSettings()
        );
    }
}
