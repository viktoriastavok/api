using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using MyApi.Data;
using MyApi.Models;
using WebApplication1.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Moq.Protected;
using System.Linq;
using System.Threading;

public class BooksControllerTests
{
    private BooksDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BooksDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new BooksDbContext(options);
    }

    private HttpClient CreateMockHttpClient(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = statusCode,
               Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
           });

        return new HttpClient(handlerMock.Object);
    }

    // --- GET /api/Books/{id} ---
    [Fact]
    public async Task GetById_ReturnsBook_WhenExists()
    {
        var context = GetInMemoryDbContext();
        var httpClient = CreateMockHttpClient(@"{ ""title"": ""Mock Book"", ""authors"": [], ""subjects"": [] }");
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var controller = new BooksController(context, Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == httpClient), memoryCache);

        var result = await controller.GetById("OL12345W");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var book = Assert.IsType<OpenLibraryBookDto>(okResult.Value);
        Assert.Equal("Mock Book", book.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenBookDoesNotExist()
    {
        var context = GetInMemoryDbContext();
        var httpClient = CreateMockHttpClient("", HttpStatusCode.NotFound);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var controller = new BooksController(context, Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == httpClient), memoryCache);
        var result = await controller.GetById("fake-id");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- POST /api/Books ---
    [Fact]
    public async Task AddToFavorites_ReturnsOk_WhenBookIsNew()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);

        var dto = new FavoriteBookDto
        {
            Id = "OL12345M",
            Title = "Test Book",
            Authors = new List<string> { "Test Author" },
            Note = "",
            Rating = null
        };

        var result = await controller.AddToFavorites(dto);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AddToFavorites_ReturnsConflict_WhenBookExists()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        context.FavoriteBooks.Add(new FavoriteBook
        {
            Id = "OL12345M",
            Title = "Test Book",
            Authors = new List<string> { "Test Author" },
            Note = "",
            Rating = null
        });
        context.SaveChanges();

        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);

        var dto = new FavoriteBookDto
        {
            Id = "OL12345M",
            Title = "Test Book",
            Authors = new List<string> { "Test Author" },
            Note = "",
            Rating = null
        };

        var result = await controller.AddToFavorites(dto);
        Assert.IsType<ConflictObjectResult>(result);
    }

    // --- PUT /api/Books/{id} ---
    [Fact]
    public async Task UpdateBook_UpdatesRating_WhenValid()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        context.FavoriteBooks.Add(new FavoriteBook
        {
            Id = "OL123",
            Title = "Test",
            Authors = new(),
            Note = "",
            Rating = null
        });
        await context.SaveChangesAsync();

        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);
        var dto = new BookUpdateDto { Rating = 5 };
        var result = await controller.UpdateBook("OL123", dto);
        var ok = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<FavoriteBook>(ok.Value);
        Assert.Equal(5, updated.Rating);
    }

    [Fact]
    public async Task UpdateBook_ReturnsNotFound_WhenBookNotExists()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);
        var dto = new BookUpdateDto { Rating = 4 };
        var result = await controller.UpdateBook("OLXXX", dto);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- DELETE /api/Books/{id} ---
    [Fact]
    public async Task DeleteFavorite_RemovesBook_WhenExists()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        context.FavoriteBooks.Add(new FavoriteBook
        {
            Id = "OL456",
            Title = "Delete Me",
            Authors = new(),
            Note = "",
            Rating = 3
        });
        await context.SaveChangesAsync();

        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);
        var result = await controller.DeleteFavorite("OL456");

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(context.FavoriteBooks.ToList());
    }

    [Fact]
    public async Task DeleteFavorite_ReturnsNotFound_WhenBookNotFound()
    {
        var context = GetInMemoryDbContext();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var controller = new BooksController(context, new Mock<IHttpClientFactory>().Object, memoryCache);

        var result = await controller.DeleteFavorite("NONEXISTENT");
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
