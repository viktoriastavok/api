using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MyApi.Models
{
    public class FavoriteBook
    {
        [Key]
        public string Id { get; set; }
        public string Title { get; set; }

        // Використовуємо string AuthorsJson замість List<string>
        public string AuthorsJson { get; set; } = "[]";

        public string? Note { get; set; }
        public int? Rating { get; set; }

        [NotMapped]
        public List<string> Authors
        {
            get => Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(AuthorsJson) ?? new();
            set => AuthorsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
        }
    }
}
