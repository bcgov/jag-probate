using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Probate.Api.Models;
using Probate.Db;
using Probate.Db.Models;

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
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly ProbateDbContext _db;

        public SubmissionService(ProbateDbContext db)
        {
            _db = db;
        }

        public async Task<SubmissionResponseDto> CreateSubmissionAsync(
            CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            var submission = new Submission
            {
                ChefsSubmissionId = dto.ChefsSubmissionId,
                ApplicantName = dto.ApplicantName,
                CreatedBy = dto.CreatedBy,
                Status = dto.Status,
                LastUpdatedAt = dto.LastUpdatedAt,
                LastFiledAt = dto.LastFiledAt,
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync(cancellationToken);

            return new SubmissionResponseDto
            {
                Id = submission.Id,
                ChefsSubmissionId = submission.ChefsSubmissionId,
                ApplicantName = submission.ApplicantName,
                CreatedBy = submission.CreatedBy,
                Status = submission.Status,
                LastUpdatedAt = submission.LastUpdatedAt,
                LastFiledAt = submission.LastFiledAt,
                CreatedAt = submission.CreatedAt,
            };
        }

        public async Task<IReadOnlyList<SubmissionResponseDto>> GetSubmissionsByUserAsync(
            string username,
            CancellationToken cancellationToken = default
        )
        {
            return await _db
                .Submissions.Where(s => s.CreatedBy == username)
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
    }
}
