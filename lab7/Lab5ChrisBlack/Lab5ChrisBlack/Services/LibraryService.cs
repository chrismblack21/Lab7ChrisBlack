using Lab5ChrisBlack.Models;
using Microsoft.VisualBasic.FileIO;

namespace Lab5ChrisBlack.Services;

public class LibraryService : ILibraryService
{
    private readonly object syncRoot = new();
    private readonly string booksFilePath;
    private readonly string usersFilePath;
    private List<Book> books = new();
    private List<User> users = new();
    private readonly Dictionary<int, List<Book>> borrowedBooks = new();

    public LibraryService(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "Data");
        booksFilePath = Path.Combine(dataDirectory, "Books.csv");
        usersFilePath = Path.Combine(dataDirectory, "Users.csv");

        EnsureDataFilesExist(dataDirectory);
        LoadBooks();
        LoadUsers();
    }

    public IReadOnlyList<Book> ReadBooks()
    {
        lock (syncRoot)
        {
            LoadBooks();
            return books.Select(CloneBook).ToList();
        }
    }

    public IReadOnlyList<User> ReadUsers()
    {
        lock (syncRoot)
        {
            LoadUsers();
            return users.Select(CloneUser).ToList();
        }
    }

    public IReadOnlyList<BookInventoryItem> GetBookInventory()
    {
        lock (syncRoot)
        {
            return BuildInventory().ToList();
        }
    }

    public IReadOnlyList<UserBorrowedBooks> GetBorrowedBooks()
    {
        lock (syncRoot)
        {
            return borrowedBooks
                .Where(entry => entry.Value.Count > 0)
                .OrderBy(entry => entry.Key)
                .Select(entry => new UserBorrowedBooks
                {
                    User = CloneUser(users.FirstOrDefault(user => user.Id == entry.Key)
                        ?? new User { Id = entry.Key, Name = $"User {entry.Key}" }),
                    Books = entry.Value.Select(CloneBook).ToList()
                })
                .ToList();
        }
    }

    public OperationResult AddBook(Book book)
    {
        lock (syncRoot)
        {
            var candidate = NormalizeBook(book);
            if (!IsValidBook(candidate))
            {
                return OperationResult.Fail("Title, author, and ISBN are required.");
            }

            candidate.Id = books.Any() ? books.Max(existingBook => existingBook.Id) + 1 : 1;
            books.Add(candidate);
            SaveBooks();

            return OperationResult.Ok($"Added \"{candidate.Title}\" to the catalog.");
        }
    }

    public OperationResult EditBook(Book book)
    {
        lock (syncRoot)
        {
            var candidate = NormalizeBook(book);
            if (!IsValidBook(candidate))
            {
                return OperationResult.Fail("Title, author, and ISBN are required.");
            }

            var existingBook = books.FirstOrDefault(currentBook => currentBook.Id == candidate.Id);
            if (existingBook is null)
            {
                return OperationResult.Fail("Book not found.");
            }

            existingBook.Title = candidate.Title;
            existingBook.Author = candidate.Author;
            existingBook.ISBN = candidate.ISBN;
            SaveBooks();

            return OperationResult.Ok($"Updated \"{candidate.Title}\".");
        }
    }

    public OperationResult DeleteBook(int id)
    {
        lock (syncRoot)
        {
            var book = books.FirstOrDefault(currentBook => currentBook.Id == id);
            if (book is null)
            {
                return OperationResult.Fail("Book not found.");
            }

            var totalCopies = books.Count(currentBook => currentBook.Id == id);
            var borrowedCopies = GetBorrowedCount(id);

            if (totalCopies <= borrowedCopies)
            {
                return OperationResult.Fail("All copies of this book are currently borrowed.");
            }

            books.Remove(book);
            SaveBooks();

            return OperationResult.Ok($"Removed one available copy of \"{book.Title}\".");
        }
    }

    public OperationResult AddUser(User user)
    {
        lock (syncRoot)
        {
            var candidate = NormalizeUser(user);
            if (!IsValidUser(candidate))
            {
                return OperationResult.Fail("A user name and valid email address are required.");
            }

            candidate.Id = users.Any() ? users.Max(existingUser => existingUser.Id) + 1 : 1;
            users.Add(candidate);
            SaveUsers();

            return OperationResult.Ok($"Added user \"{candidate.Name}\".");
        }
    }

    public OperationResult EditUser(User user)
    {
        lock (syncRoot)
        {
            var candidate = NormalizeUser(user);
            if (!IsValidUser(candidate))
            {
                return OperationResult.Fail("A user name and valid email address are required.");
            }

            var existingUser = users.FirstOrDefault(currentUser => currentUser.Id == candidate.Id);
            if (existingUser is null)
            {
                return OperationResult.Fail("User not found.");
            }

            existingUser.Name = candidate.Name;
            existingUser.Email = candidate.Email;
            SaveUsers();

            return OperationResult.Ok($"Updated user \"{candidate.Name}\".");
        }
    }

    public OperationResult DeleteUser(int id)
    {
        lock (syncRoot)
        {
            var user = users.FirstOrDefault(currentUser => currentUser.Id == id);
            if (user is null)
            {
                return OperationResult.Fail("User not found.");
            }

            if (borrowedBooks.TryGetValue(id, out var userBorrowedBooks) && userBorrowedBooks.Count > 0)
            {
                return OperationResult.Fail("Return this user's borrowed books before deleting the user.");
            }

            users.Remove(user);
            borrowedBooks.Remove(id);
            SaveUsers();

            return OperationResult.Ok($"Deleted user \"{user.Name}\".");
        }
    }

    public OperationResult BorrowBook(int userId, int bookId)
    {
        lock (syncRoot)
        {
            var user = users.FirstOrDefault(currentUser => currentUser.Id == userId);
            if (user is null)
            {
                return OperationResult.Fail("User not found.");
            }

            var book = books.FirstOrDefault(currentBook => currentBook.Id == bookId);
            if (book is null)
            {
                return OperationResult.Fail("Book not found.");
            }

            var totalCopies = books.Count(currentBook => currentBook.Id == bookId);
            var borrowedCount = GetBorrowedCount(bookId);
            if (borrowedCount >= totalCopies)
            {
                return OperationResult.Fail("No available copies remain for that book.");
            }

            if (!borrowedBooks.TryGetValue(userId, out var userBorrowedBooks))
            {
                userBorrowedBooks = new List<Book>();
                borrowedBooks[userId] = userBorrowedBooks;
            }

            userBorrowedBooks.Add(CloneBook(book));

            return OperationResult.Ok($"{user.Name} borrowed \"{book.Title}\".");
        }
    }

    public OperationResult ReturnBook(int userId, int bookId)
    {
        lock (syncRoot)
        {
            if (!borrowedBooks.TryGetValue(userId, out var userBorrowedBooks) || userBorrowedBooks.Count == 0)
            {
                return OperationResult.Fail("This user has no borrowed books to return.");
            }

            var bookIndex = userBorrowedBooks.FindIndex(book => book.Id == bookId);
            if (bookIndex < 0)
            {
                return OperationResult.Fail("Borrowed book not found for this user.");
            }

            var book = userBorrowedBooks[bookIndex];
            userBorrowedBooks.RemoveAt(bookIndex);

            if (userBorrowedBooks.Count == 0)
            {
                borrowedBooks.Remove(userId);
            }

            var userName = users.FirstOrDefault(user => user.Id == userId)?.Name ?? $"User {userId}";
            return OperationResult.Ok($"{userName} returned \"{book.Title}\".");
        }
    }

    private IEnumerable<BookInventoryItem> BuildInventory()
    {
        var borrowedCounts = borrowedBooks
            .SelectMany(entry => entry.Value)
            .GroupBy(book => book.Id)
            .ToDictionary(group => group.Key, group => group.Count());

        return books
            .GroupBy(book => book.Id)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var firstBook = group.First();
                var totalCopies = group.Count();
                borrowedCounts.TryGetValue(group.Key, out var borrowedCount);

                return new BookInventoryItem
                {
                    Id = firstBook.Id,
                    Title = firstBook.Title,
                    Author = firstBook.Author,
                    ISBN = firstBook.ISBN,
                    TotalCopies = totalCopies,
                    AvailableCopies = Math.Max(totalCopies - borrowedCount, 0)
                };
            });
    }

    private int GetBorrowedCount(int bookId) =>
        borrowedBooks.Sum(entry => entry.Value.Count(book => book.Id == bookId));

    private void EnsureDataFilesExist(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        if (!File.Exists(booksFilePath))
        {
            File.WriteAllText(booksFilePath, string.Empty);
        }

        if (!File.Exists(usersFilePath))
        {
            File.WriteAllText(usersFilePath, string.Empty);
        }
    }

    private void LoadBooks()
    {
        books = ReadCsvRows(booksFilePath, 4)
            .Select(fields => int.TryParse(fields[0], out var id)
                ? new Book
                {
                    Id = id,
                    Title = fields[1].Trim(),
                    Author = fields[2].Trim(),
                    ISBN = fields[3].Trim()
                }
                : null)
            .Where(book => book is not null)
            .Select(book => book!)
            .ToList();
    }

    private void LoadUsers()
    {
        users = ReadCsvRows(usersFilePath, 3)
            .Select(fields => int.TryParse(fields[0], out var id)
                ? new User
                {
                    Id = id,
                    Name = fields[1].Trim(),
                    Email = fields[2].Trim()
                }
                : null)
            .Where(user => user is not null)
            .Select(user => user!)
            .ToList();
    }

    private void SaveBooks()
    {
        var lines = books.Select(book =>
            string.Join(",",
                book.Id,
                EscapeCsv(book.Title),
                EscapeCsv(book.Author),
                EscapeCsv(book.ISBN)));

        File.WriteAllLines(booksFilePath, lines);
    }

    private void SaveUsers()
    {
        var lines = users.Select(user =>
            string.Join(",",
                user.Id,
                EscapeCsv(user.Name),
                EscapeCsv(user.Email)));

        File.WriteAllLines(usersFilePath, lines);
    }

    private static IEnumerable<string[]> ReadCsvRows(string path, int expectedFieldCount)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        using var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };

        parser.SetDelimiters(",");

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length < expectedFieldCount || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            yield return fields;
        }
    }

    private static string EscapeCsv(string value)
    {
        var sanitizedValue = value.Replace("\"", "\"\"");
        return sanitizedValue.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? $"\"{sanitizedValue}\""
            : sanitizedValue;
    }

    private static Book NormalizeBook(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title?.Trim() ?? string.Empty,
        Author = book.Author?.Trim() ?? string.Empty,
        ISBN = book.ISBN?.Trim() ?? string.Empty
    };

    private static User NormalizeUser(User user) => new()
    {
        Id = user.Id,
        Name = user.Name?.Trim() ?? string.Empty,
        Email = user.Email?.Trim() ?? string.Empty
    };

    private static bool IsValidBook(Book book) =>
        !string.IsNullOrWhiteSpace(book.Title) &&
        !string.IsNullOrWhiteSpace(book.Author) &&
        !string.IsNullOrWhiteSpace(book.ISBN);

    private static bool IsValidUser(User user) =>
        !string.IsNullOrWhiteSpace(user.Name) &&
        !string.IsNullOrWhiteSpace(user.Email);

    private static Book CloneBook(Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        ISBN = book.ISBN
    };

    private static User CloneUser(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email
    };
}
