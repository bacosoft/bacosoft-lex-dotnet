using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bacosoft
{
    public class NetworkException : ServiceException
    {
        public NetworkException(string message, Exception cause) : base(message, cause) { }
    }
}
