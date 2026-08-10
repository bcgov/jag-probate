using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class Submission : EntityBase
    {
        /// <summary>
        /// Public-facing opaque identifier used in URLs and API responses.
        /// </summary>
        public Guid PublicId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string ChefsSubmissionId { get; set; }

        [Required]
        [MaxLength(255)]
        public string ApplicantName { get; set; }

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; }

        [MaxLength(50)]
        public string Status { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public DateTime? LastFiledAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string SubmissionData { get; set; }

        public ICollection<StepData> StepDataEntries { get; set; } = new List<StepData>();
    }
}
