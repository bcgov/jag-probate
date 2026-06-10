using System;

namespace Probate.Api.Models
{
    public class CreateSubmissionDto
    {
        public required string ChefsSubmissionId { get; set; }
        public required string ApplicantName { get; set; }
        public required string CreatedBy { get; set; }
        public required string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public string? SubmissionData { get; set; }
    }

    public class SubmissionResponseDto
    {
        public int Id { get; set; }
        public required string ChefsSubmissionId { get; set; }
        public required string ApplicantName { get; set; }
        public required string CreatedBy { get; set; }
        public required string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SubmissionData { get; set; }
    }
}
