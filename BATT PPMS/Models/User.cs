using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BATT_PPMS.Models
{
    [Table("User")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Column("role_id")]
        public int RoleId { get; set; }

        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "Active";

        // Navigation
        [ForeignKey("RoleId")]
        public Role? Role { get; set; }
    }
}
