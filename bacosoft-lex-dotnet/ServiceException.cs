using System;
using System.Net;

namespace Bacosoft
{
    public class ServiceException : Exception
    {
        public HttpStatusCode StatusCode { get; private set; }

        public DateTime Timestamp { get; private set; }

        public ServiceException(HttpStatusCode statusCode, DateTime timestamp, string message) : base(message)
        {
            StatusCode = statusCode;
            Timestamp = timestamp;
        }

        public ServiceException(string message, Exception cause) : base(message, cause)
        {
            Timestamp = DateTime.Now;
        }
    }
}
