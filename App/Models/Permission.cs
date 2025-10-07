using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Models;

public class Permission
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    // Navigation property - One Permission belongs to one Role
    public virtual Role? Role { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}