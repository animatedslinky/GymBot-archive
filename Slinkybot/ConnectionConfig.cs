using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slinkybot
{
    public class ConnectionConfig
    {
        public string username { get; set; }
        public string oauth { get; set; }
        public string channel { get; set; }

        public ConnectionConfig()
        {
            username = "";
            oauth = "";
            channel = "";
        }
    }
}
