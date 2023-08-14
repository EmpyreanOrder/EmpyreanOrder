using System;

namespace Impostors
{
    public class ImpostorsException : Exception
    {
        public ImpostorsException(string message) : base(message)
        {
        }

        public ImpostorsException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}