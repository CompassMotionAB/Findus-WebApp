using System;

namespace Findus.Helpers
{
    public static class DateTimeExtensions
    {
        public static string ToWcDate(this DateTime? dateTime)
        {
            //return $"{dateTime:yyyy-MM-ddTHH:mm:ss}";
            return ((DateTime)dateTime).ToUniversalTime().ToString("o");
        }

        public static DateTime EndOfDay(this DateTime dateTime)
        {
            return dateTime.AddDays(1).AddTicks(-1);
        }
    }
}