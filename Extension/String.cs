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
        public static string Omit(this string value, int length, Encoding enc = null)
        {
            var bytes = enc.GetBytes(value);
            if (bytes.Length <= length) return value;
            var realSize = length - 2;

            return $"{Encoding.Default.GetString(bytes, 0, realSize)}..";
        }
    }
}
