using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTANetworkServer
{
    public class CancelEventArgs
    {
        public bool Cancel { get; set; }
        public string Reason { get; set; }

        public CancelEventArgs() { }
        public CancelEventArgs(bool cancel)
        {
            Cancel = cancel;
        }
    }
}
