using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyApi.Services
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly Dictionary<long, List<dynamic>> _searchResults = new();
        private readonly Dictionary<long, int> _currentPage = new(); // нова змінна для сторінки
        private const int PageSize = 5; // скільки книг показувати на одній сторінці


        public TelegramBotService(string token)
        {
            _botClient = new TelegramBotClient(token);
        }

        public void Start()
        {
            var cts = new CancellationTokenSource();
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                cancellationToken: cts.Token
            );
            Console.WriteLine("🤖 Бот запущено!");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                var callback = update.CallbackQuery;
                long chatId = callback.Message.Chat.Id;

                if (!_searchResults.TryGetValue(chatId, out var books)) return;

                int page = _currentPage.ContainsKey(chatId) ? _currentPage[chatId] : 0;
                int totalPages = (int)Math.Ceiling((double)books.Count / PageSize);

                if (callback.Data == "next") page = (page + 1) % totalPages;
                else if (callback.Data == "prev") page = (page - 1 + totalPages) % totalPages;

                _currentPage[chatId] = page;

                string newText = FormatPage(books, page);

                if (callback.Message.Text != newText) // уникаємо помилки EditMessageTextAsync
                {
                    await bot.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: callback.Message.MessageId,
                        text: newText,
                        replyMarkup: InlineNavKeyboard(),
                        cancellationToken: token
                    );
                }

                return;
            }

            if (update.Message?.Text == null)
                return;

            string textMsg = update.Message.Text.Trim();
            long chat = update.Message.Chat.Id;

            if (textMsg.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendTextMessageAsync(
                    chatId: chat,
                    text: "Вітаю! Ось доступні команди:\n" +
                          "/start — показати перелік команд\n" +
                          "/search <назва книги> — пошук книги за назвою чи автором \n" +
                          "/save OLID — зберегти книгу в обране\n" +
                          "/rate OLID 5 — поставити рейтинг\n" +
                          "/delete OLID — видалити з обраного\n" +
                          "/favorites — показати обране\n" +
                          "/history — показати історію пошуків",
                    cancellationToken: token
                );
                return;
            }


            if (textMsg.StartsWith("/save ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = textMsg.Split(' ', 2);
                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                {
                    await bot.SendTextMessageAsync(chat, "⚠️ Формат: /save OLID", cancellationToken: token);
                    return;
                }

                string olid = parts[1].Trim();
                using var http = new HttpClient();
                var get = await http.GetAsync($"http://localhost:5010/api/Books/{olid}");
                if (!get.IsSuccessStatusCode)
                {
                    await bot.SendTextMessageAsync(chat, "❌ Книга не знайдена , спробуйте ще раз", cancellationToken: token);
                    return;
                }

                dynamic raw = JsonConvert.DeserializeObject(await get.Content.ReadAsStringAsync());
                var authors = raw.authors != null
                    ? ((IEnumerable<dynamic>)raw.authors).Select(a => (string)a).ToList()
                    : new List<string>();

                var model = new
                {
                    id = olid,
                    title = (string)(raw.title ?? "Без назви"),
                    authors,
                    note = "",
                    rating = (int?)null
                };

                var post = await http.PostAsync(
                    "http://localhost:5010/api/Books",
                    new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json")
                );

                await bot.SendTextMessageAsync(
                    chat,
                    post.IsSuccessStatusCode ? "✅ Книгу збережено" : "❌ Не вдалося зберегти книгу, спробуйте ще раз",
                    cancellationToken: token
                );
                return;
            }


            if (textMsg.StartsWith("/rate ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = textMsg.Split(' ', 3);
                if (parts.Length < 3 || !int.TryParse(parts[2], out var rating))
                {
                    await bot.SendTextMessageAsync(chat, "⚠️ Формат: /rate OLID 5", cancellationToken: token);
                    return;
                }

                string olid = parts[1].Trim();
                var payload = new { rating };

                using var http = new HttpClient();
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var put = await http.PutAsync($"http://localhost:5010/api/Books/{olid}", content);

                await bot.SendTextMessageAsync(
                    chat,
                    put.IsSuccessStatusCode ? "⭐ Рейтинг оновлено" : "❌ Не вдалося оновити рейтинг, спробуйте ще раз",
                    cancellationToken: token
                );
                return;
            }



            // /delete OLID
            if (textMsg.StartsWith("/delete ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = textMsg.Split(' ', 2);
                if (parts.Length < 2)
                {
                    await bot.SendTextMessageAsync(chat, "⚠️ Формат: /delete OLID", cancellationToken: token);
                    return;
                }

                string olid = parts[1].Trim();
                using var http = new HttpClient();
                var del = await http.DeleteAsync($"http://localhost:5010/api/Books/{olid}");

                await bot.SendTextMessageAsync(
                    chat,
                    del.IsSuccessStatusCode ? "🗑 Книгу видалено" : "❌ Не вдалося видалити книгу, спробуйте ще раз",
                    cancellationToken: token
                );
                return;
            }


            if (textMsg.Equals("/favorites", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient();
                var res = await http.GetAsync("http://localhost:5010/api/Books/favorites");
                if (!res.IsSuccessStatusCode)
                {
                    await bot.SendTextMessageAsync(chat, "❌ Не вдалося отримати обране, спробуйте ще раз", cancellationToken: token);
                    return;
                }

                var list = JsonConvert.DeserializeObject<List<dynamic>>(await res.Content.ReadAsStringAsync());
                if (list == null || list.Count == 0)
                {
                    await bot.SendTextMessageAsync(chat, "📭 Обране порожнє", cancellationToken: token);
                    return;
                }

                var sb = new StringBuilder();
                foreach (var b in list)
                {
                    sb.AppendLine($"• {b.title}");
                    if (b.authors != null) sb.AppendLine($"  Автор(и): {string.Join(", ", b.authors)}");
                    if (b.rating != null) sb.AppendLine($"  Рейтинг: {b.rating}");
                    if (b.id != null) sb.AppendLine($"  OLID: {b.id}");
                    sb.AppendLine();
                }

                await bot.SendTextMessageAsync(chat, sb.ToString(), cancellationToken: token);
                return;
            }


            if (textMsg.Equals("/history", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient();
                var res = await http.GetAsync("http://localhost:5010/api/history");
                if (!res.IsSuccessStatusCode)
                {
                    await bot.SendTextMessageAsync(chat, "❌ Не вдалося отримати історію, спробуйте ще раз", cancellationToken: token);
                    return;
                }

                var hist = JsonConvert.DeserializeObject<List<dynamic>>(await res.Content.ReadAsStringAsync());
                if (hist == null || hist.Count == 0)
                {
                    await bot.SendTextMessageAsync(chat, "📭 Історія порожня", cancellationToken: token);
                    return;
                }

                var sb2 = new StringBuilder("🔎 Історія запитів:\n");
                foreach (var h in hist)
                    sb2.AppendLine($"• {h.query}  ({h.timestamp})");

                await bot.SendTextMessageAsync(chat, sb2.ToString(), cancellationToken: token);
                return;
            }

            // /search назва книги
            if (textMsg.StartsWith("/search ", StringComparison.OrdinalIgnoreCase))
            {
                string query = textMsg.Substring(8).Trim();
                if (string.IsNullOrEmpty(query))
                {
                    await bot.SendTextMessageAsync(chat, "⚠️ Формат: /search назва_книги", cancellationToken: token);
                    return;
                }

                await bot.SendTextMessageAsync(chat, "🔍 Шукаю книгу...", cancellationToken: token);

                using var http = new HttpClient();
                var resp = await http.GetAsync($"http://localhost:5010/api/Books/search?query={Uri.EscapeDataString(query)}");
                var list = JsonConvert.DeserializeObject<List<dynamic>>(await resp.Content.ReadAsStringAsync());

                if (list == null || list.Count == 0)
                {
                    await bot.SendTextMessageAsync(chat, "😕 Нічого не знайдено, спробуйте ще раз", cancellationToken: token);
                    return;
                }

                _searchResults[chat] = list;
                _currentPage[chat] = 0;

                string pageText = FormatPage(list, 0);
                await bot.SendTextMessageAsync(chat, pageText, replyMarkup: InlineNavKeyboard(), cancellationToken: token);
                return;
            }
            //дефолт
            try
            {
                await bot.SendTextMessageAsync(chat, "Введіть правильний запит!", cancellationToken: token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Помилка пошуку: " + ex.Message);
                await bot.SendTextMessageAsync(chat, "⚠️ Помилка при пошуку книги", cancellationToken: token);
            }
        }
        private string FormatPage(List<dynamic> books, int page)
        {
            var sb = new StringBuilder();
            int totalPages = (int)Math.Ceiling((double)books.Count / PageSize);
            int start = page * PageSize;
            int end = Math.Min(start + PageSize, books.Count);

            for (int i = start; i < end; i++)
            {
                var b = books[i];
                sb.AppendLine($"📘 {b.title}");
                if (b.authors != null) sb.AppendLine($"✍️ {string.Join(", ", b.authors)}");
                sb.AppendLine($"🆔 {b.id}\n");
            }

            sb.AppendLine($"📄 Сторінка {page + 1} з {totalPages}");
            return sb.ToString();
        }

        private InlineKeyboardMarkup InlineNavKeyboard()
            => new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️", "prev"),
                    InlineKeyboardButton.WithCallbackData("➡️", "next"),
                }
            });

        private string FormatBook(dynamic book, int idx, int total)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Книга {idx}/{total}");
            sb.AppendLine(book.title.ToString());
            if (book.authors != null)
                sb.AppendLine("Автор(и): " + string.Join(", ", book.authors));
            if (book.id != null)
                sb.AppendLine("OLID: " + book.id.ToString());
            return sb.ToString();
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            Console.WriteLine(" Bot exception: " + exception.Message);
            return Task.CompletedTask;
        }
    }
}
