using System;

namespace FortnoxApiExample.Helper
{

    public static class Utilities
    {
        public static int GetNeededPages(int pageSize, int maxPerPage, int totalInvoices)
        {
            return (int)Math.Ceiling(Math.Min(totalInvoices, maxPerPage) / (float)pageSize);
        }
        public static int GetNeededPages(int pageSize, int totalSize)
        {
            return (int)Math.Ceiling(totalSize / (float)pageSize);
        }

        public static string AppendQueryString(this string str, string query = null, string delimiter = ";")
        {
            if (!string.IsNullOrEmpty(query))
            {
                return str + delimiter + System.Web.HttpUtility.ParseQueryString(query);
            }
            return str;
        }

        public static string[] GetQueryParams(this string str, string delimiter = ";")
        {
            return str.Split(delimiter);
        }
    }
}