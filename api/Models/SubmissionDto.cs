using System;

namespace Probate.Api.Models
{
    public class CreateSubmissionDto
    {
        public Guid? PublicId { get; set; }
        public string ChefsSubmissionId { get; set; }
        public string ApplicantName { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
    }

    public class SubmissionResponseDto
    {
        public Guid PublicId { get; set; }
        public string ChefsSubmissionId { get; set; }
        public string ApplicantName { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime? LastFiledAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
