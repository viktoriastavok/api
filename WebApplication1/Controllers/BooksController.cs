using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyApi.Data;
using MyApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly BooksDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public BooksController(BooksDbContext context, IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
            _cache = cache;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string query)
        {
            try
            {
                var url = $"https://openlibrary.org/search.json?q={query}";

                if (!_cache.TryGetValue(url, out string content))
                {
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                        return BadRequest("Помилка під час звернення до OpenLibrary");

                    content = await response.Content.ReadAsStringAsync();
                    _cache.Set(url, content, TimeSpan.FromMinutes(15));
                }

                dynamic data = JsonConvert.DeserializeObject(content);
                var books = new List<OpenLibraryBookDto>();

                foreach (var doc in ((JArray)data.docs).Take(20))
                {
                    string olid = doc["key"]?.ToString();
                    string workUrl = $"https://openlibrary.org{olid}.json";

                    if (!_cache.TryGetValue(workUrl, out string workContent))
                    {
                        var workResponse = await _httpClient.GetAsync(workUrl);
                        if (!workResponse.IsSuccessStatusCode) continue;

                        workContent = await workResponse.Content.ReadAsStringAsync();
                        _cache.Set(workUrl, workContent, TimeSpan.FromMinutes(10));
                    }

                    dynamic work = JsonConvert.DeserializeObject(workContent);

                    string description = null;
                    if (work.description != null)
                    {
                        description = work.description.Type == JTokenType.Object
                            ? work.description.value?.ToString()
                            : work.description.ToString();
                    }

                    List<string> authorNames = new();
                    if (work.authors != null)
                    {
                        try
                        {
                            var authorRefs = JsonConvert.DeserializeObject<List<AuthorRef>>(Convert.ToString(work.authors));
                            authorNames = await GetAuthorNamesAsync(authorRefs);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("⚠️ Неможливо отримати авторів: " + ex.Message);
                        }
                    }

                    List<string> subjects = new();
                    if (work.subjects != null)
                    {
                        try
                        {
                            subjects = ((JArray)work.subjects).Select(s => s.ToString()).ToList();
                        }
                        catch { }
                    }

                    books.Add(new OpenLibraryBookDto
                    {
                        Id = olid.Replace("/works/", ""),
                        Title = work.title,
                        Description = description,
                        Authors = authorNames,
                        Subjects = subjects
                    });
                }

                _context.SearchHistories.Add(new SearchHistory
                {
                    Query = query,
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(books);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Помилка у Search(): " + ex.Message);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("{olid}")]
        public async Task<IActionResult> GetById(string olid)
        {
            var url = $"https://openlibrary.org/works/{olid}.json";

            if (!_cache.TryGetValue(url, out string content))
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return NotFound("Книгу не знайдено");

                content = await response.Content.ReadAsStringAsync();
                _cache.Set(url, content, TimeSpan.FromMinutes(10));
            }

            dynamic work = JsonConvert.DeserializeObject(content);

            string description = null;
            if (work.description != null)
            {
                description = work.description.Type == JTokenType.Object
                    ? work.description.value?.ToString()
                    : work.description.ToString();
            }

            var authorRefs = JsonConvert.DeserializeObject<List<AuthorRef>>(Convert.ToString(work.authors));
            var authorNames = await GetAuthorNamesAsync(authorRefs);

            return Ok(new OpenLibraryBookDto
            {
                Id = olid,
                Title = work.title,
                Description = description,
                Authors = authorNames,
                Subjects = work.subjects != null
                    ? ((JArray)work.subjects).Select(s => s.ToString()).ToList()
                    : new List<string>()
            });
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var favorites = await _context.FavoriteBooks.ToListAsync();
            return Ok(favorites);
        }

        [HttpPost]
        public async Task<IActionResult> AddToFavorites([FromBody] FavoriteBookDto model)
        {
            if (_context.FavoriteBooks.Any(b => b.Id == model.Id))
                return Conflict("Книга вже додана");

            var book = new FavoriteBook
            {
                Id = model.Id,
                Title = model.Title,
                Authors = model.Authors,
                Note = model.Note,
                Rating = model.Rating
            };

            _context.FavoriteBooks.Add(book);
            await _context.SaveChangesAsync();

            return Ok("Книгу збережено.");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(string id, [FromBody] BookUpdateDto updates)
        {
            var book = await _context.FavoriteBooks.FindAsync(id);
            if (book == null)
                return NotFound($"Книга з OLID '{id}' не знайдена.");

            if (updates.Rating.HasValue)
                book.Rating = updates.Rating;

            if (!string.IsNullOrWhiteSpace(updates.Note))
                book.Note = updates.Note;

            await _context.SaveChangesAsync();
            return Ok(book);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFavorite(string id)
        {
            var book = await _context.FavoriteBooks.FindAsync(id);
            if (book == null)
                return NotFound("Книга не знайдена");

            _context.FavoriteBooks.Remove(book);
            await _context.SaveChangesAsync();

            return Ok("Книгу видалено з обраного");
        }

        [HttpGet("/api/history")]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _context.SearchHistories
                .OrderByDescending(h => h.Timestamp)
                .Take(10)
                .ToListAsync();

            return Ok(history);
        }

        private async Task<List<string>> GetAuthorNamesAsync(List<AuthorRef> authorRefs)
        {
            var authors = new List<string>();

            foreach (var authorRef in authorRefs)
            {
                var url = $"https://openlibrary.org{authorRef.Author.Key}.json";

                if (_cache.TryGetValue(url, out string content))
                {
                    var data = JsonConvert.DeserializeObject<AuthorDto>(content);
                    if (data?.Name != null)
                        authors.Add(data.Name);
                    continue;
                }

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var authorContent = await response.Content.ReadAsStringAsync();
                    _cache.Set(url, authorContent, TimeSpan.FromMinutes(10));

                    var data = JsonConvert.DeserializeObject<AuthorDto>(authorContent);
                    if (data?.Name != null)
                        authors.Add(data.Name);
                }
            }

            return authors;
        }
    }
}
