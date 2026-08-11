using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Models;
using Probate.Api.Options;
using Probate.Db.Models;
using Refit;

namespace Probate.Api.Services
{
    public interface ISubmissionService
    {
        Task<SubmissionResponseDto> CreateSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        );
        Task<SubmissionResponseDto?> GetSubmissionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default
        );
        Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        );
        Task<SubmissionResponseDto> UpsertSubmissionAsync(
            CreateSubmissionDto dto,
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
        private readonly ILogger<SubmissionService> _logger;
        private readonly ChefsOptions _options;
        private readonly IChefsApi _chefsApi;

        public SubmissionService(
            ProbateDbContext db,
            ILogger<SubmissionService> logger,
            IChefsApi chefsApi,
            IOptions<ChefsOptions> options
        )
        {
            _db = db;
            _logger = logger;
            _chefsApi = chefsApi;
            _options = options.Value;
        }

        public async Task<SubmissionResponseDto> CreateSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            var submission = dto.Adapt<Submission>();
            // Ensure a real PublicId is assigned; Mapster may leave it as Guid.Empty
            // if dto.PublicId is null (first save).
            if (submission.PublicId == Guid.Empty)
                submission.PublicId = Guid.NewGuid();

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
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

        public async Task<SubmissionResponseDto> UpsertSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            Submission? existing = null;

            // Primary lookup by PublicId
            if (dto.PublicId.HasValue)
            {
                existing = await _db.Submissions.FirstOrDefaultAsync(
                    s => s.PublicId == dto.PublicId.Value && s.DeletedAt == null,
                    cancellationToken
                );
            }

            if (existing != null)
            {
                existing.ChefsSubmissionId = dto.ChefsSubmissionId;
                existing.ApplicantName = dto.ApplicantName;
                existing.Status = dto.Status;
                existing.LastUpdatedAt = dto.LastUpdatedAt;
                existing.LastFiledAt = dto.LastFiledAt;
                existing.SubmissionData = dto.SubmissionData;
            }
            else
            {
                existing = dto.Adapt<Submission>();
                // Ensure a real PublicId is assigned for new records.
                if (existing.PublicId == Guid.Empty)
                    existing.PublicId = Guid.NewGuid();
                _db.Submissions.Add(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return existing.Adapt<SubmissionResponseDto>();
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
                ChefsSubmissionId = string.Empty,
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
                await _db
                    .Submissions.Include(s => s.StepDataEntries)
                    .FirstOrDefaultAsync(
                        s => s.PublicId == publicId && s.DeletedAt == null,
                        cancellationToken
                    )
                ?? throw new KeyNotFoundException($"Submission {publicId} not found.");

            submission.SubmissionData = compiledData;
            submission.Status = "submitted";
            submission.LastFiledAt = DateTime.UtcNow;
            submission.LastUpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            // Submit each step to CHEFS for platform visibility (best-effort).
            foreach (var step in submission.StepDataEntries.Where(s => !string.IsNullOrWhiteSpace(s.Data)))
            {
                if (
                    !_options.Forms.TryGetValue(step.FormId, out var formOptions)
                    || string.IsNullOrWhiteSpace(formOptions?.FormId)
                )
                {
                    _logger.LogWarning(
                        "No CHEFS form configured for step {FormId}, skipping CHEFS submission",
                        step.FormId
                    );
                    continue;
                }

                try
                {
                    var parsedData = Newtonsoft.Json.JsonConvert.DeserializeObject(step.Data);
                    await _chefsApi.CreateSubmissionAsync(
                        formOptions.FormId,
                        new Infrastructure.Chefs.ChefsCreateSubmissionRequest
                        {
                            Draft = false,
                            Submission = new Infrastructure.Chefs.ChefsSubmissionPayload
                            {
                                Data = parsedData,
                            },
                        },
                        cancellationToken
                    );
                }
                catch (ApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "CHEFS submission failed for step {FormId} on submission {PublicId}: {StatusCode}",
                        step.FormId,
                        publicId,
                        ex.StatusCode
                    );
                }
            }

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

            // Soft delete locally first
            submission.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            // Then delete from CHEFS — get formId from options
            // TODO step based forms will need to handle multiple form ids.
            if (
                !_options.Forms.TryGetValue("legal", out var formOptions)
                || string.IsNullOrWhiteSpace(formOptions?.FormId)
            )
                throw new InvalidOperationException("Form key 'legal' is not configured.");

            try
            {
                await _chefsApi.DeleteSubmissionAsync(
                    formOptions.FormId,
                    submission.ChefsSubmissionId,
                    cancellationToken
                );
            }
            catch (ApiException ex)
            {
                _logger.LogWarning(
                    ex,
                    "CHEFS delete failed for submission {ChefsSubmissionId}: {StatusCode}",
                    submission.ChefsSubmissionId,
                    ex.StatusCode
                );
                // Local soft delete already done — log but don't throw
                // so the user's record is still hidden even if CHEFS delete fails
            }
        }
    }
}
