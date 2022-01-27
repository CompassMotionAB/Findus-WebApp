using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fortnox.SDK.Auth;
using Newtonsoft.Json;

namespace FortnoxApiExample.Helper
{
    public static class Utilities
    {
        public static T LoadJson<T>(string jsonFilePath, JsonConverter jsonConverter = null)
        {
            using (StreamReader r = new StreamReader(jsonFilePath))
            {
                string json = r.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(json, jsonConverter);
            }
        }
        public static String JoinToLower(this IEnumerable<string> list, String delimiter)
        {
            StringBuilder sb = new StringBuilder();
            foreach (String item in list)
            {
                sb.Append(item.ToLower());
            }
            return sb.ToString();
        }
        public static String JoinToLower(this IEnumerable<Scope> list, String delimiter)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Scope item in list)
            {
                sb.Append(item.ToString().ToLower());
            }
            return sb.ToString();
        }

        /// <summary>
		/// Gets the hash code for the contents of the array since the default hash code
		/// for an array is unique even if the contents are the same.
		/// </summary>
		/// <remarks>
		/// See Jon Skeet (C# MVP) response in the StackOverflow thread 
		/// http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
		/// </remarks>
		/// <param name="array">The array to generate a hash code for.</param>
		/// <returns>The hash code for the values in the array.</returns>
		public static int GetHashCode(Scope[] array)
        {
            // if non-null array then go into unchecked block to avoid overflow
            if (array != null)
            {
                unchecked
                {
                    int hash = 17;

                    // get hash code for all items in array
                    foreach (var item in array)
                    {
                        hash = hash * 23 + item.GetHashCode();
                    }

                    return hash;
                }
            }

            // if null, hash code is zero
            return 0;
        }
    }
}