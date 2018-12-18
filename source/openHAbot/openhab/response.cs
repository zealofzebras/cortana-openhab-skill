using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace openHAbot.openhab
{
    public class Entities
    {
        public string @object { get; set; }
    }

    public class Intent
    {
        public string name { get; set; }
        public Entities entities { get; set; }
    }

    public class Response
    {
        public string language { get; set; }
        public string query { get; set; }
        public string answer { get; set; }
        public string hint { get; set; }
        public Intent intent { get; set; }
    }
}
