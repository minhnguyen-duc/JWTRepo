using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JwtAuthDemo.Models;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required]
    [StringLength(50)]
    public string Username { get; set; }
    [Required]
    public string Password { get; set; } // Never store passwords like this in production!
    public string Role { get; set; } = "User";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    // Navigation property 
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; }
}