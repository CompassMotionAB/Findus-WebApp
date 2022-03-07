using System.Collections.Generic;

namespace Findus.Helpers
{
    public static class DictionaryExtensions
    {
        public static void Increment<T>(this Dictionary<T, int> dictionary, T key)
        {
            dictionary.TryGetValue(key, out int count);
            dictionary[key] = count + 1;
        }
    }
}