using System;
using System.ComponentModel.DataAnnotations;

namespace Probate.Db.Models
{
    public class User : EntityBase
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Required]
        [MaxLength(255)]
        public string Email { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? ADId { get; set; }

        [MaxLength(100)]
        public string ADUsername { get; set; }

        public string UserGuid { get; set; }
    }
}
