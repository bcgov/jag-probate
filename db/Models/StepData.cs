using System;
using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class StepData : EntityBase
    {
        /// <summary>
        /// Public-facing opaque identifier for this step data row.
        /// </summary>
        public Guid PublicId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Logical form/step key (e.g. "step1", "step3") — references the config key.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FormId { get; set; }

        /// <summary>
        /// JSON blob containing this step's form data.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// FK to the parent Submission.
        /// </summary>
        public int SubmissionId { get; set; }

        /// <summary>
        /// Navigation property to the parent Submission.
        /// </summary>
        public Submission Submission { get; set; }

        /// <summary>
        /// The CHEFS form version at the time this step data was saved,
        /// to protect against form version transitions.
        /// </summary>
        [MaxLength(50)]
        public string FormVersion { get; set; }
    }
}
