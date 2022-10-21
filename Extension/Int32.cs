using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBot.Extension
{
    public static class Int32Extension
    {
        public static string ToEmoji(this int value)
        {
            switch(value)
            {
                case 0: return ":zero:";
                case 1: return ":one:";
                case 2: return ":two:";
                case 3: return ":three:";
                case 4: return ":four:";
                case 5: return ":five:";
                case 6: return ":six:";
                case 7: return ":seven:";
                case 8: return ":eight:";
                case 9: return ":nine:";
                case 10: return ":ten:";
                default: return value.ToString();
            }
        }

        public static TimeSpan ToSecond(this int value)
        {
            return TimeSpan.FromSeconds(value);
        }
    }
}
