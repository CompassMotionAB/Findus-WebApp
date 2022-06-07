using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Findus.Helpers
{

    public class DateComparer : IComparer<DateTime> {
        public int Compare(DateTime lhs, DateTime rhs) {
            return DateTime.Compare(lhs, rhs);
        }
    }
    public static class CurrencyUtils
    {

        public static async Task<decimal> GetSEKCurrencyRateAsync(DateTime date, string currency, HttpClient httpClient)
        {
            if(currency == "SEK-SEK") return 1M;

            string dateStringFrom = String.Format("{0:yyyy-M-d}", date.AddDays(-7));
            string dateStringTo = String.Format("{0:yyyy-M-d}", date);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync($"https://www.riksbank.se/sv/statistik/sok-rantor--valutakurser/?c=cAverage&f=Day&from={dateStringFrom}&g130-SEK{currency}PMI=on&s=Dot&to={dateStringTo}&export=csv");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to fetch currency: SEK-{currency}, for date: {dateStringTo} - {ex.Message}");
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

            var sortedDictonary = new SortedDictionary<DateTime, string>(currencies, new DateComparer());

            var currencyRate = sortedDictonary.Values.Last();

            return decimal.Parse(currencyRate);
        }
    }
}