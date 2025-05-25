using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MyApi.Models
{
 

    public class SearchHistory
    {
        public int Id { get; set; }
        public string Query { get; set; }
        public DateTime Timestamp { get; set; }
    }
}



