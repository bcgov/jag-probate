using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Models;
using Probate.Db.Models;

namespace Probate.Api.Services
{
    /// <summary>
    /// Compiled step data bound to the exact owned submission row it came from.
    /// </summary>
    public sealed record CompiledSubmissionData(int SubmissionId, string Data);

    public interface IStepDataService
    {
        /// <summary>Atomically claims the owned parent before upserting a step; never writes a new legacy username.</summary>
        Task<StepDataResponseDto> UpsertStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            UpsertStepDataDto dto,
            CancellationToken cancellationToken = default
        );

        /// <summary>Reads and claims the live parent using its authoritative ID or null-ID legacy fallback.</summary>
        Task<StepDataResponseDto?> GetStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            string formId,
            CancellationToken cancellationToken = default
        );

        /// <summary>Reads all steps after claiming the owned live parent.</summary>
        Task<IReadOnlyList<StepDataResponseDto>> GetAllStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>Claims the owned live parent and compiles its steps; never reads another owner's data.</summary>
        Task<string> GetCompiledDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Compiles steps and returns the internal ID of the exact owned submission row.
        /// Use that ID for subsequent writes instead of resolving a potentially duplicate public ID again.
        /// </summary>
        Task<CompiledSubmissionData> GetCompiledSubmissionDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        );
    }

    public class StepDataService : IStepDataService
    {
        private readonly ProbateDbContext _db;
        private readonly ILogger<StepDataService> _logger;

        public StepDataService(ProbateDbContext db, ILogger<StepDataService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<StepDataResponseDto> UpsertStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            UpsertStepDataDto dto,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == submissionPublicId,
                cancellationToken
            ) ?? throw new KeyNotFoundException($"Submission {submissionPublicId} not found.");

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

            // Extract deceased name from step data to keep the submission row searchable.
            if (!string.IsNullOrWhiteSpace(dto.Data))
            {
                try
                {
                    var parsed = JObject.Parse(dto.Data);
                    var name = parsed.Value<string>("deceasedName");
                    if (!string.IsNullOrWhiteSpace(name))
                        submission.ApplicantName = name;
                }
                catch (JsonReaderException)
                {
                    // Non-critical — skip name extraction if JSON is malformed.
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            return existing.Adapt<StepDataResponseDto>();
        }

        /// <inheritdoc />
        public async Task<StepDataResponseDto?> GetStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            string formId,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == submissionPublicId,
                cancellationToken
            );

            if (submission == null)
                return null;

            var stepData = await _db.StepData.FirstOrDefaultAsync(
                sd => sd.SubmissionId == submission.Id && sd.FormId == formId,
                cancellationToken
            );

            return stepData?.Adapt<StepDataResponseDto>();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<StepDataResponseDto>> GetAllStepDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == submissionPublicId,
                cancellationToken
            );

            if (submission == null)
                throw new KeyNotFoundException($"Submission {submissionPublicId} not found.");

            return await _db
                .StepData.Where(sd => sd.SubmissionId == submission.Id)
                .OrderBy(sd => sd.FormId)
                .Select(sd => sd.Adapt<StepDataResponseDto>())
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Merges all step data rows into a single JSON object.
        /// Each step's data is merged at the top level (flat merge, last-write-wins by step order).
        /// Claims legacy ownership atomically before reading the parent's steps.
        /// </summary>
        public async Task<string> GetCompiledDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        ) =>
            (
                await GetCompiledSubmissionDataAsync(submissionPublicId, owner, cancellationToken)
            ).Data;

        /// <inheritdoc />
        public async Task<CompiledSubmissionData> GetCompiledSubmissionDataAsync(
            Guid submissionPublicId,
            SubmissionOwner owner,
            CancellationToken cancellationToken = default
        )
        {
            var submission = await SubmissionOwnership.FindAndClaimAsync(
                _db,
                owner,
                s => s.PublicId == submissionPublicId,
                cancellationToken
            ) ?? throw new KeyNotFoundException($"Submission {submissionPublicId} not found.");

            var stepDataRows = await _db
                .StepData.Where(sd => sd.SubmissionId == submission.Id)
                .OrderBy(sd => sd.FormId)
                .ToListAsync(cancellationToken);

            var compiled = new JObject();

            foreach (var row in stepDataRows.Where(r => !string.IsNullOrWhiteSpace(r.Data)))
            {
                try
                {
                    var stepJson = JObject.Parse(row.Data);
                    compiled.Merge(
                        stepJson,
                        new JsonMergeSettings
                        {
                            MergeArrayHandling = MergeArrayHandling.Replace,
                            MergeNullValueHandling = MergeNullValueHandling.Merge,
                        }
                    );
                }
                catch (JsonReaderException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to parse step data JSON for step {FormId} on submission {PublicId}",
                        row.FormId,
                        submissionPublicId
                    );
                }
            }

            return new CompiledSubmissionData(
                submission.Id,
                compiled.ToString(Newtonsoft.Json.Formatting.None)
            );
        }
    }
}
