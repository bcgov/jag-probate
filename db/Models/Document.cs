using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class Document : EntityBase
    {
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [MaxLength(100)]
        public string FileType { get; set; }

        public long FileSize { get; set; }

        public string FilePath { get; set; }

        // Foreign key
        public int CaseId { get; set; }

        // Navigation property
        public Case Case { get; set; }
    }
}
