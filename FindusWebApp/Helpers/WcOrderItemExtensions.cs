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

        private static async Task<List<WcOrder>> GetPage(this WCObject.WCOrderItem wcOrderApi, DateTime? dateAfter = null, DateTime? dateBefore = null, string dateStr = null, int pageNumber = 1, int itemPerPage = 100)
        {
            if(dateAfter == null && dateBefore == null) {
                if(string.IsNullOrEmpty(dateStr)) {
                    throw new ArgumentNullException(nameof(dateStr));
                }
                dateAfter = DateTime.Parse(dateStr);
                dateBefore = dateAfter;
            } else {
                dateAfter ??= dateBefore;
                dateBefore ??= dateAfter;
            }
            var orders = await wcOrderApi.GetAll(new Dictionary<string, string>() {
                    {"page", pageNumber.ToString()},
                    {"per_page", itemPerPage.ToString()},
                    {"after", dateAfter.ToWcDate()},
                    {"before", dateBefore.ToWcDate()},
                    {"status", "completed"}
                });
            // NOTE: Temporary fix to remove unexpected orders outside date range
            //orders.RemoveAll(i => i.date_paid > dateBefore || i.date_paid < dateAfter);
            if(orders.Count == 0) throw new Exception("Invalid Orders returned from WooCommerce");
            return orders;
        }
        public static async Task<List<WcOrder>> GetOrders(this WCObject.WCOrderItem wcOrderApi, string dateFrom = null, string dateTo = null, IMemoryCache memoryCache = null)
        {

            const int maxPerPage = 100; //Max
            const int numPages = 1;//int numPages = HttpUtilities.GetNeededPages(pageSize: 25, maxPerPage);


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
                throw new ArgumentException($"Expected dateFrom and dateTo to be both be either null or defined. Received: dateFrom: {dateFrom}, dateTo:{dateTo}");
                //dateAfter = DateTime.Now.AddDays(-1).EndOfDay().AddTicks(1);
                //dateBefore = DateTime.Now.EndOfDay();
            }
            else
            {
                dateAfter = DateTime.ParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                dateBefore = DateTime.ParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture).EndOfDay();
            }

            if (memoryCache == null)
            {
                return await wcOrderApi.GetPage(dateAfter, dateBefore);
            }

            var cacheKey = $"{dateAfter:yyyy-MM-dd}_{dateBefore:yyyy-MM-dd}-orders-{numPages}x{maxPerPage}";

            if (!memoryCache.TryGetValue(cacheKey, out List<WcOrder> orders))
            {
                orders = await wcOrderApi.GetPage(
                    dateAfter,
                    dateBefore,
                    pageNumber: numPages,
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