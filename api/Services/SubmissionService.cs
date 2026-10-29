using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Probate.Api.Models;
using Probate.Db.Models;

namespace Probate.Api.Services
{
    public interface ISubmissionService
    {
        Task<SubmissionResponseDto?> GetSubmissionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default
        );
        Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        );
        Task DeleteSubmissionAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a bare draft submission for the step-based wizard flow.
        /// </summary>
        Task<SubmissionResponseDto> CreateDraftSubmissionAsync(
            string username,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Compiles all step data into the submission record and marks it as submitted.
        /// </summary>
        Task<SubmissionResponseDto> FinalizeSubmissionAsync(
            Guid publicId,
            string compiledData,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Returns a single submission including its raw stored form data.
        /// Throws <see cref="KeyNotFoundException"/> if not found or soft-deleted.
        /// </summary>
        Task<Submission> GetSubmissionByIdAsync(
            int id,
            string username,
            CancellationToken cancellationToken = default
        );
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly ProbateDbContext _db;

        public SubmissionService(ProbateDbContext db)
        {
            _db = db;
        }

        public async Task<SubmissionResponseDto?> GetSubmissionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.PublicId == id && s.DeletedAt == null,
                cancellationToken
            );
            return submission?.Adapt<SubmissionResponseDto>();
        }

        public async Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        )
        {
            return await _db
                .Submissions.Where(s => s.CreatedBy == username && s.DeletedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .ProjectToType<SubmissionResponseDto>()
                .ToListAsync(cancellationToken);
        }

        public async Task<Submission> GetSubmissionByIdAsync(
            int id,
            string username,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.Id == id && s.CreatedBy == username && s.DeletedAt == null,
                cancellationToken
            );

            if (submission is null)
                throw new KeyNotFoundException($"Submission {id} not found.");

            return submission;
        }

        public async Task<SubmissionResponseDto> CreateDraftSubmissionAsync(
            string username,
            CancellationToken cancellationToken = default
        )
        {
            var submission = new Submission
            {
                PublicId = Guid.NewGuid(),
                ApplicantName = string.Empty,
                CreatedBy = username,
                Status = "draft",
                LastUpdatedAt = DateTime.UtcNow,
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
        }

        public async Task<SubmissionResponseDto> FinalizeSubmissionAsync(
            Guid publicId,
            string compiledData,
            CancellationToken cancellationToken = default
        )
        {
            var submission =
                await _db.Submissions.FirstOrDefaultAsync(
                    s => s.PublicId == publicId && s.DeletedAt == null,
                    cancellationToken
                ) ?? throw new KeyNotFoundException($"Submission {publicId} not found.");

            submission.SubmissionData = compiledData;
            submission.Status = "submitted";
            submission.LastFiledAt = DateTime.UtcNow;
            submission.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
        }

        public async Task DeleteSubmissionAsync(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            var submission =
                await _db.Submissions.FirstOrDefaultAsync(s => s.PublicId == id, cancellationToken)
                ?? throw new KeyNotFoundException($"Submission {id} not found.");

            submission.DeletedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
