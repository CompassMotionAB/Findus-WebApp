using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v2;
using Findus.Helpers;
using System.Linq;

namespace FindusWebApp.Helpers
{
    public static class WcOrderItemExtensions
    {
        private static readonly MemoryCacheEntryOptions _orderCacheOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromHours(8));

        private static async Task<List<WcOrder>> GetPage(this WCObject.WCOrderItem wcOrderApi, DateTime? dateAfter = null, DateTime? dateBefore = null, int pageNumber = 1, int itemPerPage = 100, string dateStr = null)
        {
            if (dateAfter == null && dateBefore == null)
            {
                if (string.IsNullOrEmpty(dateStr))
                {
                    throw new ArgumentNullException(nameof(dateStr));
                }
                dateAfter = DateTime.Parse(dateStr);
                dateBefore = dateAfter;
            }
            else
            {
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
            if (orders.Count == 0) throw new Exception("Invalid Orders returned from WooCommerce");
            return orders;
        }
        public static async Task<List<WcOrder>> GetOrders(this WCObject.WCOrderItem wcOrderApi, string dateFrom = null, string dateTo = null, IMemoryCache memoryCache = null, int pageNumber = 1)
        {
            const int itemsPerPage = 100; //Max: 100

            dateFrom ??= dateTo;
            dateTo ??= dateFrom;

            if (string.IsNullOrEmpty(dateFrom))
            {
                throw new ArgumentNullException(nameof(dateFrom));
            }

            var dateAfter = DateTime.ParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var dateBefore = DateTime.ParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture).EndOfDay();

            if (memoryCache == null)
            {
                return await wcOrderApi.GetPage(dateAfter, dateBefore);
            }

            var cacheKey = $"{dateAfter:yyyy-MM-dd}_{dateBefore:yyyy-MM-dd}-orders-{pageNumber}x{itemsPerPage}";

            if (!memoryCache.TryGetValue(cacheKey, out List<WcOrder> orders))
            {
                //NOTE: Offset date to capture all orders within acceptable limit
                var dateAfterOffset = dateAfter;
                dateAfterOffset.AddDays(-12);
                orders = await wcOrderApi.GetPage(
                    dateAfterOffset,
                    dateBefore,
                    pageNumber,
                    itemsPerPage);

                while (orders.Count >= itemsPerPage * pageNumber)
                {
                    pageNumber++;
                    orders = orders.Concat(
                        await wcOrderApi.GetPage(
                            dateAfterOffset,
                            dateBefore,
                            pageNumber,
                            itemsPerPage
                        )
                    ).ToList();
                }

                //NOTE: Ensures that order payments falls within expected time period
                orders.RemoveAll(o => o.date_paid < dateAfter || o.date_paid > dateBefore);

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