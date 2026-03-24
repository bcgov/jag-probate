using System;
using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class Submission : EntityBase
    {
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
    }
}
