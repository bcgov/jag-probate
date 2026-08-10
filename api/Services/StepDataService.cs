using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Probate.Api.Models;
using Probate.Db.Models;

namespace Probate.Api.Services
{
    public interface IStepDataService
    {
        Task<StepDataResponseDto> UpsertStepDataAsync(
            Guid submissionPublicId,
            UpsertStepDataDto dto,
            CancellationToken cancellationToken = default
        );

        Task<StepDataResponseDto?> GetStepDataAsync(
            Guid submissionPublicId,
            string formId,
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<StepDataResponseDto>> GetAllStepDataAsync(
            Guid submissionPublicId,
            CancellationToken cancellationToken = default
        );

        Task<string> GetCompiledDataAsync(
            Guid submissionPublicId,
            CancellationToken cancellationToken = default
        );
    }

    public class StepDataService : IStepDataService
    {
        private readonly ProbateDbContext _db;
        private readonly ILogger<StepDataService> _logger;

        public StepDataService(
            ProbateDbContext db,
            ILogger<StepDataService> logger
        )
        {
            _db = db;
            _logger = logger;
        }

        public async Task<StepDataResponseDto> UpsertStepDataAsync(
            Guid submissionPublicId,
            UpsertStepDataDto dto,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.PublicId == submissionPublicId && s.DeletedAt == null,
                cancellationToken
            ) ?? throw new KeyNotFoundException(
                $"Submission {submissionPublicId} not found."
            );

            var existing = await _db.StepData.FirstOrDefaultAsync(
                sd => sd.SubmissionId == submission.Id && sd.FormId == dto.FormId,
                cancellationToken
            );

            if (existing != null)
            {
                existing.Data = dto.Data;
                existing.FormVersion = dto.FormVersion;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                existing = new StepData
                {
                    PublicId = Guid.NewGuid(),
                    FormId = dto.FormId,
                    Data = dto.Data,
                    SubmissionId = submission.Id,
                    FormVersion = dto.FormVersion,
                };
                _db.StepData.Add(existing);
            }

            submission.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return existing.Adapt<StepDataResponseDto>();
        }

        public async Task<StepDataResponseDto?> GetStepDataAsync(
            Guid submissionPublicId,
            string formId,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.PublicId == submissionPublicId && s.DeletedAt == null,
                cancellationToken
            );

            if (submission == null) return null;

            var stepData = await _db.StepData.FirstOrDefaultAsync(
                sd => sd.SubmissionId == submission.Id && sd.FormId == formId,
                cancellationToken
            );

            return stepData?.Adapt<StepDataResponseDto>();
        }

        public async Task<IReadOnlyList<StepDataResponseDto>> GetAllStepDataAsync(
            Guid submissionPublicId,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.PublicId == submissionPublicId && s.DeletedAt == null,
                cancellationToken
            );

            if (submission == null) return Array.Empty<StepDataResponseDto>();

            return await _db.StepData
                .Where(sd => sd.SubmissionId == submission.Id)
                .OrderBy(sd => sd.FormId)
                .Select(sd => sd.Adapt<StepDataResponseDto>())
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Merges all step data rows into a single JSON object.
        /// Each step's data is merged at the top level (flat merge, last-write-wins by step order).
        /// </summary>
        public async Task<string> GetCompiledDataAsync(
            Guid submissionPublicId,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await _db.Submissions.FirstOrDefaultAsync(
                s => s.PublicId == submissionPublicId && s.DeletedAt == null,
                cancellationToken
            ) ?? throw new KeyNotFoundException(
                $"Submission {submissionPublicId} not found."
            );

            var stepDataRows = await _db.StepData
                .Where(sd => sd.SubmissionId == submission.Id)
                .OrderBy(sd => sd.FormId)
                .ToListAsync(cancellationToken);

            var compiled = new JObject();

            foreach (var row in stepDataRows)
            {
                if (string.IsNullOrWhiteSpace(row.Data)) continue;

                try
                {
                    var stepJson = JObject.Parse(row.Data);
                    compiled.Merge(stepJson, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Replace,
                        MergeNullValueHandling = MergeNullValueHandling.Merge
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to parse step data JSON for step {FormId} on submission {PublicId}",
                        row.FormId,
                        submissionPublicId
                    );
                }
            }

            return compiled.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
