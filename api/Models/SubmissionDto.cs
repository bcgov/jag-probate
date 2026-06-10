using System;

namespace Probate.Api.Models
{
    public class CreateSubmissionDto
    {
        public string ChefsSubmissionId { get; set; }
        public string ApplicantName { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public string? SubmissionData { get; set; }
    }

    public class SubmissionResponseDto
    {
        public int Id { get; set; }
        public string ChefsSubmissionId { get; set; }
        public string ApplicantName { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SubmissionData { get; set; }
    }
}
