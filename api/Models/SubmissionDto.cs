using System;

namespace Probate.Api.Models
{
    public class SubmissionResponseDto
    {
        public Guid PublicId { get; set; }
        public int Id { get; set; }
        public required string ApplicantName { get; set; }

        /// <summary>
        /// Deprecated legacy username, retained for compatibility; null on new rows.
        /// Owner user IDs are never included in submission responses.
        /// </summary>
        public string? CreatedBy { get; set; }
        public required string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SubmissionData { get; set; }
    }
}
