using System;
using System.Collections.Generic;
using System.Text;

namespace FortnoxApiExample.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Joins an IEnumerable of T with optional delimiter.
        /// </summary>
        /// <remarks>
        /// Delimiter will also be turned into lowercase
		/// </remarks>
		/// <param name="list">The IEnumerable<T> to join as lowercase.</param>
		/// <returns>String of IEnumerable<T> with optional delimiter.</returns>
        public static string JoinToLower<T>(this IEnumerable<T> list, string delimiter = null)
        {
            using IEnumerator<T> iterator = list.GetEnumerator();
            if (!iterator.MoveNext())
            {
                return "";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(iterator.Current);
            while (iterator.MoveNext())
            {
                if (!String.IsNullOrEmpty(delimiter))
                    sb.Append(delimiter);
                sb.Append(iterator.Current);
            }
            return sb.ToString().ToLower();
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
		public static int GetUniqueHashForEnumerable<T>(this IEnumerable<T> array)
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
                        hash = (hash * 23) + item.GetHashCode();
                    }

                    return hash;
                }
            }

            // if null, hash code is zero
            return 0;
        }
    }
}