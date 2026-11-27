using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Models;
using Probate.Db.Models;

namespace Probate.Api.Services
{
    public interface ISubmissionService
    {
        /// <summary>Reads and claims an owned live row using the authoritative ID or null-ID legacy fallback.</summary>
        Task<SubmissionResponseDto?> GetSubmissionByIdAsync(
            Guid id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>Lists and claims owned live rows; username fallback applies only to null owner IDs.</summary>
        Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>Claims before deleting an owned row; preserves the deprecated username.</summary>
        Task DeleteSubmissionAsync(
            Guid id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Creates a draft with only CreatedByUserId; never populates deprecated CreatedBy.
        /// </summary>
        Task<SubmissionResponseDto> CreateDraftSubmissionAsync(
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Compiles all step data into the submission record and marks it as
        /// <see cref="SubmissionStatus.ReadyToPrint"/>. The submission stays
        /// editable; <c>LastFiledAt</c> is only set once the package is eFiled.
        /// Claims legacy ownership atomically before changing the submission.
        /// </summary>
        Task<SubmissionResponseDto> FinalizeSubmissionAsync(
            Guid publicId,
            SubmissionOwner owner,
            string compiledData,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Returns a single submission including its raw stored form data.
        /// Throws <see cref="KeyNotFoundException"/> if not found or soft-deleted.
        /// Claims the legacy row before reading; CreatedByUserId is authoritative.
        /// </summary>
        Task<Submission> GetSubmissionByIdAsync(
            int id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Status codes stored in <see cref="Submission.Status"/>.
    /// </summary>
    public static class SubmissionStatus
    {
        public const string Draft = "draft";

        /// <summary>
        /// All steps completed and data compiled; forms can be printed. The
        /// application remains open for editing until it is eFiled.
        /// </summary>
        public const string ReadyToPrint = "ready_to_print";
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly ProbateDbContext _db;

        public SubmissionService(ProbateDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc />
        public async Task<SubmissionResponseDto?> GetSubmissionByIdAsync(
            Guid id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == id,
                cancellationToken
            );
            return submission?.Adapt<SubmissionResponseDto>();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submissions = await SubmissionOwnership.ListAndClaimAsync(
                _db,
                owner,
                cancellationToken
            );
            return submissions.Adapt<List<SubmissionResponseDto>>();
        }

        /// <inheritdoc />
        public async Task<Submission> GetSubmissionByIdAsync(
            int id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.Id == id,
                cancellationToken
            );

            if (submission is null)
                throw new KeyNotFoundException($"Submission {id} not found.");

            return submission;
        }

        /// <inheritdoc />
        public async Task<SubmissionResponseDto> CreateDraftSubmissionAsync(
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = new Submission
            {
                PublicId = Guid.NewGuid(),
                ApplicantName = string.Empty,
                CreatedByUserId = owner.UserId,
                Status = SubmissionStatus.Draft,
                LastUpdatedAt = DateTime.UtcNow,
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
        }

        /// <inheritdoc />
        public async Task<SubmissionResponseDto> FinalizeSubmissionAsync(
            Guid publicId,
            SubmissionOwner owner,
            string compiledData,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == publicId,
                cancellationToken
            ) ?? throw new KeyNotFoundException($"Submission {publicId} not found.");

            submission.SubmissionData = compiledData;
            submission.Status = SubmissionStatus.ReadyToPrint;
            submission.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
        }

        /// <inheritdoc />
        public async Task DeleteSubmissionAsync(
            Guid id,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == id,
                cancellationToken,
                includeDeleted: true
            ) ?? throw new KeyNotFoundException($"Submission {id} not found.");

            submission.DeletedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
