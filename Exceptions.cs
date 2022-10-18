using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBot
{
    public class CommandInvalidException : Exception
    {
        public CommandInvalidException() { }
        public CommandInvalidException(string message)
            : base(message) { }
        public CommandInvalidException(string message, Exception inner)
            : base(message, inner) { }
    }
}
