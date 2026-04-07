using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
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
        Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        );
        Task<SubmissionResponseDto> UpsertSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        );
        Task DeleteSubmissionAsync(int id, CancellationToken cancellationToken = default);
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

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync(cancellationToken);

            return submission.Adapt<SubmissionResponseDto>();
        }

        public async Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        )
        {
            return await _db
                .Submissions.Where(s => s.CreatedBy == username && s.DeletedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SubmissionResponseDto
                {
                    Id = s.Id,
                    ChefsSubmissionId = s.ChefsSubmissionId,
                    ApplicantName = s.ApplicantName,
                    CreatedBy = s.CreatedBy,
                    Status = s.Status,
                    LastUpdatedAt = s.LastUpdatedAt,
                    LastFiledAt = s.LastFiledAt,
                    CreatedAt = s.CreatedAt,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<SubmissionResponseDto> UpsertSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            _logger.LogInformation(
                "Upsert called with ChefsSubmissionId: {ChefsSubmissionId}",
                dto.ChefsSubmissionId
            );
            var existing = await _db.Submissions.FirstOrDefaultAsync(
                s => s.ChefsSubmissionId == dto.ChefsSubmissionId,
                cancellationToken
            );
            _logger.LogInformation("Existing record found: {Found}", existing != null);

            if (existing != null)
            {
                // Update existing record
                existing.ApplicantName = dto.ApplicantName;
                existing.Status = dto.Status;
                existing.LastUpdatedAt = dto.LastUpdatedAt;
                existing.LastFiledAt = dto.LastFiledAt;
            }
            else
            {
                existing = dto.Adapt<Submission>();
                _db.Submissions.Add(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return existing.Adapt<SubmissionResponseDto>();
        }

        public async Task DeleteSubmissionAsync(
            int id,
            CancellationToken cancellationToken = default
        )
        {
            var submission =
                await _db.Submissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException($"Submission {id} not found.");

            // Soft delete locally first
            submission.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            // Then delete from CHEFS — get formId from options
            if (!_options.Forms.TryGetValue("legal", out var formGuid))
                throw new InvalidOperationException("Form key 'legal' is not configured.");

            try
            {
                await _chefsApi.DeleteSubmissionAsync(
                    formGuid,
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
