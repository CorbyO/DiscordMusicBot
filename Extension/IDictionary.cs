using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MusicBot.Extension
{
    public static class IDictionary
    {
        /// <summary>
        /// 딕셔너리에서 키를 통해 값을 가져옵니다.<br/>
        /// 값이 없다면 생성합니다.
        /// </summary>
        /// <param name="dic"></param>
        /// <param name="key"></param>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public static TValue ForceGet<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
            where TValue : new()
        {
            if(dic.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                value = new TValue();
                dic.Add(key, value);
                return value;
            }
        }
    }
}