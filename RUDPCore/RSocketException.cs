using System;

namespace RUDPCore
{
    public class RSocketException : Exception
    {
        public RSocketException(string message):base(message) { }
    }
}
