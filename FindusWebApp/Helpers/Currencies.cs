using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace FindusWebApp.Helpers
{
    public static class Currencies
    {
        private static async Task<decimal> GetSEKCurrencyRateAsync(DateTime date, string currency, IMemoryCache _memoryCache, HttpClient httpClient)
        {
            string dateStringFrom = String.Format("{0:yyyy-M-d}", date.AddDays(-7));
            string dateStringTo = String.Format("{0:yyyy-M-d}", date);
            string dateStringNow = String.Format("{0:yyyy-M-d}", DateTime.Now);

            var cacheKey = String.Format("{0}_{1}_{2}", dateStringTo, currency, dateStringNow);

            if (!_memoryCache.TryGetValue(cacheKey, out string currencyRate))
            {
                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync($"https://www.riksbank.se/sv/statistik/sok-rantor--valutakurser/?c=cAverage&f=Day&from={dateStringFrom}&g130-SEK{currency}PMI=on&s=Dot&to={dateStringTo}&export=csv");
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception(String.Format("Failed to fetch currency (SEK-{0}) for date({1). HttpRequestException: {2}", currency, dateStringTo, ex.Message));
                }

                //csv = await response.Content.ReadAsStringAsync();
                var csvStream = await response.Content.ReadAsStreamAsync();

                var currencies = new Dictionary<DateTime, string>();

                using (var streamReader = new StreamReader(csvStream, Encoding.UTF8, true, 512))
                {
                    String line;
                    streamReader.ReadLine(); // Skip First Row
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        var l = line.Split(";");
                        if (l[3] != "n/a")
                        {
                            currencies.Add(DateTime.Parse(l[0]), l[3]);
                        }
                    }
                }

                currencyRate = currencies.Values.Last();

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(8));
                _memoryCache.Set(cacheKey, currencyRate, cacheEntryOptions);
            }

            return decimal.Parse(currencyRate);
        }
    }
}