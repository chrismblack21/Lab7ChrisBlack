using System.ComponentModel.DataAnnotations;

namespace Lab5ChrisBlack.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;
}