using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AgileInspect
{
    public class SaveEvent
    {
        public DateTime timestampUtc { get; set; } = DateTime.UtcNow;
        public string eventType {  get; set; }
        public string clientName { get; set; }
        public string customerId { get; set; }
        public JToken data { get; set; }

    }
}
