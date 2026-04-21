using Lab5ChrisBlack.Models;

namespace Lab5ChrisBlack.Services;

public interface ILibraryService
{
    IReadOnlyList<Book> ReadBooks();
    IReadOnlyList<User> ReadUsers();
    IReadOnlyList<BookInventoryItem> GetBookInventory();
    IReadOnlyList<UserBorrowedBooks> GetBorrowedBooks();

    OperationResult AddBook(Book book);
    OperationResult EditBook(Book book);
    OperationResult DeleteBook(int id);

    OperationResult AddUser(User user);
    OperationResult EditUser(User user);
    OperationResult DeleteUser(int id);

    OperationResult BorrowBook(int userId, int bookId);
    OperationResult ReturnBook(int userId, int bookId);
}