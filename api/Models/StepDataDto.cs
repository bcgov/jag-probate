using System;

namespace Probate.Api.Models
{
    public class UpsertStepDataDto
    {
        public required string FormId { get; set; }
        public string Data { get; set; }
        public string FormVersion { get; set; }
    }

    public class StepDataResponseDto
    {
        public Guid PublicId { get; set; }
        public required string FormId { get; set; }
        public string Data { get; set; }
        public string FormVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
