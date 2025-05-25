using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MyApi.Models
{
    public class OpenLibrarySearchResult
    {
        public List<OpenLibraryDoc> Docs { get; set; }
    }

    public class OpenLibraryDoc
    {
        public string Title { get; set; }
        public List<string> AuthorName { get; set; }
        public int? FirstPublishYear { get; set; }
        public int? CoverI { get; set; }
        public string Key { get; set; }
    }
}