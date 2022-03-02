using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v2;
using Findus.Helpers;

namespace FindusWebApp.Helpers
{
    public static class WcOrderItemExtensions
    {
        private static readonly MemoryCacheEntryOptions _orderCacheOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromHours(8));
        public static async Task<List<WcOrder>> GetPages(this WCObject.WCOrderItem wcOrderApi, DateTime dateAfter, DateTime dateBefore, int numPages = 1, int itemPerPage = 16)
        {
            return await wcOrderApi.GetAll(new Dictionary<string, string>() {
                    {"page", numPages.ToString()},
                    {"per_page", itemPerPage.ToString()},
                    {"after", $"{dateAfter:yyyy-MM-ddTHH:mm:ss}"},
                    {"before", $"{dateBefore:yyyy-MM-ddTHH:mm:ss}"},
                    {"status", "completed"}
                });
        }
        public static async Task<List<WcOrder>> GetOrders(this WCObject.WCOrderItem wcOrderApi, string dateFrom = null, string dateTo = null, IMemoryCache memoryCache = null)
        {
            const int maxPerPage = 100; //Max
            int numPages = 1;//HttpUtilities.GetNeededPages(pageSize: 25, maxPerPage);

            bool noFrom = string.IsNullOrEmpty(dateFrom);
            bool noTo = string.IsNullOrEmpty(dateTo);
            if (noFrom != noTo)
            {
                throw new ArgumentException($"Expected dateFrom and dateTo to be both be either null or defined. Received: dateFrom: {dateFrom}, dateTo:{dateTo}");
            }

            DateTime dateAfter;
            DateTime dateBefore;

            if (noFrom && noTo)
            {
                dateAfter = DateTime.Now.AddDays(-7);
                dateBefore = DateTime.Now;
            }
            else
            {
                dateAfter = DateTime.ParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                dateBefore = DateTime.ParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture).EndOfDay();
            }

            if (memoryCache == null)
            {
                return await wcOrderApi.GetPages(dateAfter, dateBefore);
            }

            var cacheKey = $"{dateAfter:yyyy-MM-dd}_{dateBefore:yyyy-MM-dd}-orders-{numPages}x{maxPerPage}";

            if (!memoryCache.TryGetValue(cacheKey, out List<WcOrder> orders))
            {
                orders = await wcOrderApi.GetPages(
                    dateAfter,
                    dateBefore,
                    numPages: numPages,
                    itemPerPage: maxPerPage);

                memoryCache.Set(cacheKey, orders, _orderCacheOptions);
            }
            return orders;
        }
        public static async Task<WcOrder> Get(this WCObject.WCOrderItem wcOrderApi, ulong? orderId)
        {
            if (orderId == null)
            {
                throw new ArgumentNullException(paramName: nameof(orderId));
            }
            return await wcOrderApi.Get((int)orderId);
        }
    }
}