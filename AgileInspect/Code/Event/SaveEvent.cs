using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgileInspect
{
    public class SaveEvent
    {
        public string eventType {  get; set; }
        public string clientName { get; set; }
        public string customerId { get; set; }
        public object data { get; set; }

    }
}
