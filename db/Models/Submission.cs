using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable enable annotations

namespace Probate.Db.Models
{
    public class Submission : EntityBase
    {
        /// <summary>
        /// Public-facing opaque identifier used in URLs and API responses.
        /// </summary>
        public Guid PublicId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        public string ApplicantName { get; set; }

        /// <summary>
        /// Deprecated legacy username fallback, used only when CreatedByUserId is null.
        /// New writes leave this null. Preserve existing values until all legacy rows
        /// are claimed; the fallback and column are planned for a later removal.
        /// </summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Authoritative Keycloak sub owner ID. Null only for unclaimed legacy rows.
        /// New submissions populate only this owner field, never CreatedBy.
        /// </summary>
        [Column("created_by_user_id")]
        [MaxLength(255)]
        public string? CreatedByUserId { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public DateTime? LastFiledAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? SubmissionData { get; set; }

        public ICollection<StepData> StepDataEntries { get; set; } = new List<StepData>();
    }
}
