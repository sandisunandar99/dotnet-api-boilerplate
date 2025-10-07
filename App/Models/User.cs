using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace App.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Fullname { get; set; } = string.Empty;

    [Required]
    public bool IsActive { get; set; } = true;


    [Required]
    /**
    * Foreign key to Role Table
    * Role Id = 99 -> Admin
    * Role Id = 1 -> User
    * Role Id = 2 -> Guest
    */
    public int RoleId { get; set; } = 2;

    // Navigation property - One User belongs to one Role
    public virtual Role? Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
