using System.ComponentModel.DataAnnotations;

namespace Lab5ChrisBlack.Models;

public class Book
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Author { get; set; } = string.Empty;

    [Required]
    public string ISBN { get; set; } = string.Empty;
}