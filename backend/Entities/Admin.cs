using System.ComponentModel.DataAnnotations;

namespace BdServices.Api.Entities;

public class Admin
{
    public int Id { get; set; }

    [Required]
    public string FullName { get; set; } = "";

    [Required]
    public string Email { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = "Admin";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}