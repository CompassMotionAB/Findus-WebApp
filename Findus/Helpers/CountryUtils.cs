using System;
using System.Globalization;

namespace Findus.Helpers
{
    public static class CountryUtils
    {
        public static string GetEnglishName(string countryCode)
        {
            try
            {
                return new RegionInfo(countryCode).EnglishName;
            }
            catch (Exception)
            {
                return $"Missing English Name: {countryCode}";
            }
        }
    }
}