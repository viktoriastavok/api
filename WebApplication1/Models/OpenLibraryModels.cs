namespace MyApi.Models
{
       public class AuthorRef
    {
        public AuthorKey Author { get; set; }
    }

    public class AuthorKey
    {
        public string Key { get; set; }
    }

    public class AuthorDto
    {
        public string Name { get; set; }
    }
}
