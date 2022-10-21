using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBot.Extension
{
    public static class StringExtension
    {
        public static string Omit(this string value, int length)
        {
            if (value.Length <= length)
                return value;
            
            return value.Substring(0, length - 1) + '…';
        }

        public static string Remove(this string value, string remove)
        {
            StringBuilder sb = new();

            var start = remove[0];
            var end = remove[1];

            bool inRange = false;
            foreach (var c in value)
            {
                if (c == start) inRange = true;
                else if (c == end) 
                { 
                    inRange = false; 
                    continue; 
                }

                if (inRange) continue;

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
