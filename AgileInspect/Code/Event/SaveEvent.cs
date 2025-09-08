using System;

namespace AgileInspect
{
    public class SaveEvent
    {
        public DateTime createdAt { get; set; }
        public string eventType { get; set; }
        public string clientName { get; set; }
        public string customerId { get; set; }
        public object data { get; set; }

    }
}
