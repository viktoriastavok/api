using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

   namespace MyApi.Models
{
    public class OpenLibraryBookDto
    {
        public string Id { get; set; }                 
        public string Title { get; set; }              
        public string Description { get; set; }         
        public List<string> Authors { get; set; }       
        public List<string> Subjects { get; set; }      
    }
}
