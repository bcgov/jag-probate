using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class Case : EntityBase
    {
        [Required]
        [MaxLength(100)]
        public string CaseNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(50)]
        public string Status { get; set; }

        public DateTime? FiledDate { get; set; }

        public string Description { get; set; }

        // Navigation properties
        public List<Document> Documents { get; set; } = new();
    }
}
