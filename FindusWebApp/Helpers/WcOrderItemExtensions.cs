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
using Findus.Models;

namespace FindusWebApp.Helpers
{
    public static class WcOrderItemExtensions
    {
        private static readonly MemoryCacheEntryOptions _orderCacheOptions =
            new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30));

        private static async Task<List<WcOrder>> GetPage(
            this WCObject.WCOrderItem wcOrderApi,
            DateTime? dateAfter = null,
            DateTime? dateBefore = null,
            int pageNumber = 1,
            int itemPerPage = 100,
            string dateStr = null,
            string orderStatus = "completed"
        )
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
            var orders = await wcOrderApi.GetAll(
                new Dictionary<string, string>()
                {
                    { "page", pageNumber.ToString() },
                    { "per_page", itemPerPage.ToString() },
                    { "after", dateAfter.ToWcDate() },
                    { "before", dateBefore.ToWcDate() },
                    { "status", orderStatus }
                }
            );
            // NOTE: Temporary fix to remove unexpected orders outside date range
            //orders.RemoveAll(i => i.date_paid > dateBefore || i.date_paid < dateAfter);
            if (orders.Count == 0)
                throw new Exception("No Orders returned from WooCommerce");
            return orders;
        }

        public static async Task<List<WcOrder>> GetOrders(
            this WCObject.WCOrderItem wcOrderApi,
            string dateFrom = null,
            string dateTo = null,
            IMemoryCache memoryCache = null,
            int pageNumber = 1,
            string orderStatus = "completed",
            string cacheKeyData = ""
        )
        {
            if (memoryCache is null)
                throw new ArgumentNullException(nameof(memoryCache));

            const int itemsPerPage = 100; //Max: 100

            dateFrom ??= dateTo;
            dateTo ??= dateFrom;

            if (string.IsNullOrEmpty(dateFrom))
            {
                throw new ArgumentNullException(nameof(dateFrom));
            }

            var dateAfter = DateTime.ParseExact(
                dateFrom,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
            var dateBefore = DateTime
                .ParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .EndOfDay();

            var cacheKey = $"{dateAfter:yyyy-MM-dd}_{dateBefore:yyyy-MM-dd}-orders{cacheKeyData}";

            if (!memoryCache.TryGetValue(cacheKey, out List<WcOrder> orders))
            {
                //NOTE: Offset date to capture all orders within acceptable limit
                var dateAfterOffset = dateAfter;
                dateAfterOffset.AddDays(-12);
                orders = new List<WcOrder>(itemsPerPage);
                orders = await wcOrderApi.GetPage(
                    dateAfterOffset,
                    dateBefore,
                    pageNumber,
                    itemsPerPage,
                    orderStatus: orderStatus
                );

                while (orders.Count >= itemsPerPage * pageNumber)
                {
                    pageNumber++;
                    orders.EnsureCapacity(itemsPerPage * pageNumber);
                    orders = orders
                        .Concat(
                            await wcOrderApi.GetPage(
                                dateAfterOffset,
                                dateBefore,
                                pageNumber,
                                itemsPerPage,
                                orderStatus: orderStatus
                            )
                        )
                        .ToList();
                }

                //NOTE: Ensures that order payments falls within expected time period
                orders.RemoveAll(o => o.date_paid < dateAfter || o.date_paid > dateBefore);
                orders.TrimExcess();

                // Store order id with date
                if (dateAfter == dateBefore)
                {
                    orders.ForEach((o) => memoryCache.Set(o.id, dateAfter, _orderCacheOptions));
                }

                memoryCache.Set(cacheKey, orders, _orderCacheOptions);
            }

            return orders;
        }

        private static WcOrder TryGetCachedOrder(string orderId, IMemoryCache memoryCache)
        {
            if (
                memoryCache != null
                && memoryCache.TryGetValue(orderId, out string dateAfter)
                && memoryCache.TryGetValue(
                    $"{dateAfter:yyyy-MM-dd}_{dateAfter:yyyy-MM-dd}-orders",
                    out List<WcOrder> orders
                )
            )
            {
                return orders.First((o) => o.id.ToString() == orderId);
            }
            return null;
        }

        public static async Task<WcOrder> GetOrder(
            this WCObject.WCOrderItem wcOrderApi,
            OrderRouteModel orderRoute,
            IMemoryCache memoryCache = null
        )
        {
            // Try to get order from cache
            var order = TryGetCachedOrder(orderRoute.OrderId, memoryCache);

            return order
                ?? await wcOrderApi.GetOrder(orderRoute.OrderId, orderRoute.Status, memoryCache);
        }

        public static async Task<WcOrder> GetOrder(
            this WCObject.WCOrderItem wcOrderApi,
            string orderId,
            string orderStatus = "completed",
            IMemoryCache memoryCache = null
        )
        {
            if (orderId == null)
            {
                throw new ArgumentNullException(paramName: nameof(orderId));
            }
            if (memoryCache != null)
            {
                if (memoryCache.TryGetValue(orderId, out WcOrder cachedOrder))
                {
                    return cachedOrder;
                }
            }
            var order = await wcOrderApi.Get(
                Convert.ToInt32(orderId),
                new Dictionary<string, string> { { "status", orderStatus } }
            );
            if (order?.status != orderStatus)
            {
                if (order != null)
                {
                    throw new Exception(
                        $"Unexpected order status: '{order.status}' for order id: {order.id}\nExpected status: {orderStatus}"
                    );
                }
                else if (order?.id != Convert.ToUInt64(orderId))
                {
                    throw new Exception(
                        $"Order Id does not match: ${order?.id}, expected: ${orderId}"
                    );
                }
                else
                {
                    throw new Exception(
                        $"WooCommerce: Failed to get order: {orderId}, with status: ${orderStatus}"
                    );
                }
            }
            memoryCache?.Set(orderId, order, _orderCacheOptions);
            return order;
        }

        public static async Task<IEnumerable<WcOrder>> TryGetPartialRefundedOrders(
            this WCObject.WCOrderItem wcOrderApi,
            OrderRouteModel orderRoute,
            IMemoryCache memoryCache = null,
            int pageNumber = 1,
            string orderStatus = "completed"
        )
        {
            if (!orderRoute.IsValid())
                throw new Exception("Order route is not valid.");
            List<WcOrder> orders;
            if (orderRoute.HasDateRange())
            {
                orders = await wcOrderApi.GetOrders(
                    orderRoute.DateFrom,
                    orderRoute.DateTo,
                    memoryCache,
                    pageNumber,
                    orderStatus
                );
            }
            else
            {
                orders = new List<WcOrder> { await wcOrderApi.Get(orderRoute.OrderId) };
            }
            return orders.FindAll(o => o.refunds?.Count > 0);
        }

        public static async Task<IEnumerable<WcOrder>> GetPartialRefundedOrders(
            this WCObject.WCOrderItem wcOrderApi,
            string dateFrom = null,
            string dateTo = null,
            IMemoryCache memoryCache = null,
            int pageNumber = 1,
            string orderStatus = "completed"
        )
        {
            var orders = await wcOrderApi.GetOrders(
                dateFrom,
                dateTo,
                memoryCache,
                pageNumber,
                orderStatus
            );
            return orders.FindAll(o => o.refunds?.Count > 0);
        }

        public static async Task AddInvoiceReferenceAsync(
            this WCObject.WCOrderItem wcOrderApi,
            string orderId,
            long? invoiceNumber
        )
        {
            await wcOrderApi.Update(
                Convert.ToInt32(orderId),
                new WcOrder
                {
                    meta_data = new List<OrderMeta>
                    {
                        new OrderMeta { key = "_fortnox_invoice_number", value = invoiceNumber, }
                    }
                }
            );
        }
    }
}
