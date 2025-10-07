using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Models;

public class Role
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property - One Role can have many Users
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    // Navigation property - One Role can have many Permissions
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
