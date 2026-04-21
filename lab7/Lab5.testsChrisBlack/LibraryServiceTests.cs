using System;
using System.IO;
using System.Linq;
using Lab5ChrisBlack.Models;
using Lab5ChrisBlack.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
 [TestClass]
public class LibraryServiceTests
{
    private LibraryService CreateService(out string tempPath)
    {
        tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        var env = new FakeEnvironment
        {
            ContentRootPath = tempPath
        };

        return new LibraryService(env);
    }

    [TestMethod]
    public void AddBook_ShouldAddBook()
    {
        // Arrange
        var service = CreateService(out _);
        var book = new Book { Title = "Test", Author = "Author", ISBN = "123" };

        // Act
        var result = service.AddBook(book);
        var books = service.ReadBooks();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, books.Count());
        Assert.AreEqual("Test", books.First().Title);
    }

    [TestMethod]
    public void AddBook_InvalidBook_ShouldFail()
    {
        // Arrange
        var service = CreateService(out _);
        var book = new Book { Title = "", Author = "", ISBN = "" };

        // Act
        var result = service.AddBook(book);
        var books = service.ReadBooks();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, books.Count());
    }

    [TestMethod]
    public void ReadBooks_Empty_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService(out _);

        // Act
        var books = service.ReadBooks();

        // Assert
        Assert.AreEqual(0, books.Count());
    }

    [TestMethod]
    public void EditBook_ShouldUpdate()
    {
        // Arrange
        var service = CreateService(out _);
        var book = new Book { Title = "Old", Author = "A", ISBN = "111" };
        service.AddBook(book);

        var existing = service.ReadBooks().First();
        existing.Title = "Updated";

        // Act
        var result = service.EditBook(existing);
        var updated = service.ReadBooks().First();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Updated", updated.Title);
    }

    [TestMethod]
    public void DeleteBook_ShouldRemove()
    {
        // Arrange
        var service = CreateService(out _);
        var book = new Book { Title = "Test", Author = "A", ISBN = "111" };
        service.AddBook(book);

        var id = service.ReadBooks().First().Id;

        // Act
        var result = service.DeleteBook(id);
        var books = service.ReadBooks();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, books.Count());
    }

    [TestMethod]
    public void DeleteBook_NotFound_ShouldFail()
    {
        // Arrange
        var service = CreateService(out _);

        // Act
        var result = service.DeleteBook(999);

        // Assert
        Assert.IsFalse(result.Success);
    }
}

class FakeEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}