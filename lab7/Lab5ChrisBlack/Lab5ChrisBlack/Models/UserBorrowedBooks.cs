namespace Lab5ChrisBlack.Models;

public class UserBorrowedBooks
{
    public User User { get; set; } = new();
    public List<Book> Books { get; set; } = new();
}