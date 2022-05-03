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
                return countryCode.ToUpper() switch
                {
                    "SE" => "Sverige",
                    "CZ" => "Czech Republic",
                    _ => new RegionInfo(countryCode).EnglishName
                };
            }
            catch (Exception)
            {
                return $"Missing English Name for {countryCode}";
            }
        }
    }
}
