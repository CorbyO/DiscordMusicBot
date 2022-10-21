using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBot.Extension
{
    public static class TimeSpanExtension
    {
        public static string ToMMSS(this TimeSpan value)
        {
            return value.ToString(@"mm\:ss");
        }
    }
}
