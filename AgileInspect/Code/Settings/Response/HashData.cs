using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgileInspect.Code.Settings.Response
{
        public class HashData
        {
            public int code { get; set; }
            public Hash data { get; set; }
        }  
        public class Hash
        {
            public string hash { get; set; }
        }
}
