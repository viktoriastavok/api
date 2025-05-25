namespace MyApi.Models
{
    public class FavoriteBookDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> Authors { get; set; } = new();  
        public string Note { get; set; }
        public int? Rating { get; set; }
    }
}
