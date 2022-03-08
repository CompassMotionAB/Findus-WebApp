using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Findus.Helpers;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v2;
using System.Net.Http;
using Findus.Models;
using System.Linq;
using Newtonsoft.Json;
using FindusWebApp.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using FindusWebApp.Helpers;
using Microsoft.Extensions.Options;
using Fortnox.SDK.Entities;
using FindusWebApp.Extensions;

namespace FindusWebApp.Controllers
{
    public class VerificationResult
    {
        public InvoiceAccrual InvoiceAccrual;

        public string ErrorMessage;

        public VerificationResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public VerificationResult(InvoiceAccrual invoiceAccrual, string errorMessage = null)
        {
            InvoiceAccrual = invoiceAccrual;
            ErrorMessage = errorMessage;
        }
    }
    public class FindusController : Controller
    {
        private readonly WooKeys _wcKeys;
        private readonly WCObject.WCOrderItem _wcOrderApi;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _orderCacheOptions;
        private readonly AccountsModel _accounts;
        private readonly dynamic _coupons;
        private OrderViewModel _orderViewModel;

        public FindusController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<WooKeys> wcKeysOptions)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _orderCacheOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromHours(8));

            _wcKeys = wcKeysOptions.Value;

            if (string.IsNullOrEmpty(_wcKeys.Key) || string.IsNullOrEmpty(_wcKeys.Secret) || string.IsNullOrEmpty(_wcKeys.Url))
            {
                ViewBag.Error = "Missing WooKeys Configuration, see appsettings.sample.json";
            }
            else
            {
                var restJwt = new RestAPI(_wcKeys.Url, _wcKeys.Key, _wcKeys.Secret, false);
                _wcOrderApi = new WCObject.WCOrderItem(restJwt);
            }
            _accounts = new AccountsModel(
                    JsonUtilities.LoadJson<Dictionary<string, AccountModel>>("VATAccounts.json"),
                    JsonUtilities.LoadJson<Dictionary<string, AccountModel>>("SalesAccounts.json")
                );
            _coupons = JsonUtilities.LoadJson<dynamic>("Coupons.json");

            ViewBag.CultureInfo = new System.Globalization.CultureInfo("sv-SE");
        }

        public async Task<ActionResult> Index(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            if (string.IsNullOrEmpty(_wcKeys.Key) || string.IsNullOrEmpty(_wcKeys.Secret) || string.IsNullOrEmpty(_wcKeys.Url))
            {
                ViewBag.Error = "Missing WooKeys Configuration, see appsettings.sample.json";
                return View("Findus");
            }
            return RedirectToAction("Verification", new { orderId, dateFrom, dateTo });
        }

        private async Task<decimal> GetCurrencyRate(WcOrder order, decimal? accurateTotal = null)
        {
            if (VerificationUtils.GetPaymentMethod(order) == "Stripe")
            {
                bool stripeCharge = (string)order.meta_data.Find(d => d.key == "_stripe_charge_captured").value == "yes";
                decimal stripeFee = decimal.Parse((string)order.meta_data.Find(d => d.key == "_stripe_fee").value);
                decimal stripeNet = decimal.Parse((string)order.meta_data.Find(d => d.key == "_stripe_net").value);
                string stripeCurrency = (string)order.meta_data.Find(d => d.key == "_stripe_currency").value;

                if (!stripeCharge)
                {
                    throw new Exception("Unexpected: _stripe_charge_captured == false");
                }
                if (stripeCurrency != "SEK")
                {
                    throw new Exception($"Stripe Payment with currency: {stripeCurrency} is unsupported");
                }

                if (stripeFee <= 0.0M || stripeNet <= 0.0M)
                {
                    throw new Exception($"Stripe Fee or Net is empty or invalid, Fee: {stripeFee}, Net: {stripeNet}");
                }

                ViewData["stripeFee"] = stripeFee;
                ViewData["stripeNet"] = stripeNet;

                var total = accurateTotal ?? order.GetAccurateTotal();
                var currencyRate = (stripeFee + stripeNet) / total;

                ViewData["totalSek"] = total * currencyRate;
                ViewData["currencyRate"] = currencyRate;

                return currencyRate;
            }
            DateTime date = (DateTime)order.date_paid;
            var httpClient = _httpClientFactory.CreateClient();
            return await CurrencyUtils.GetSEKCurrencyRateAsync(date, order.currency.ToUpper(), httpClient);
        }

        private async Task<Dictionary<ulong, VerificationResult>> VerifyOrders(OrderRouteModel orderRoute, bool simplify = true)
        {
            var result = new Dictionary<ulong, VerificationResult>();
            var orders = await Get(orderRoute);
            _orderViewModel = new OrderViewModel(orders, orderRoute);
            foreach (var order in orders)
            {
                try
                {
                    var invoiceAccrual = await Verify(order, simplify);

                    result.Add((ulong)order.id, new VerificationResult(invoiceAccrual));
                }
                catch (Exception ex)
                {
                    result.Add((ulong)order.id, new VerificationResult(errorMessage: ex.Message));
                }
            }

            return result;
        }

        private async Task<ActionResult> VerifyOrder(List<WcOrder> orders, OrderRouteModel orderRoute, bool simplify = true)
        {
            try
            {
                _orderViewModel = new OrderViewModel(orders, orderRoute);
                WcOrder order = _orderViewModel.GetOrder();

                TempData["InvoiceAccrual"] = await Verify(order, simplify);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View("Findus", _orderViewModel);
        }

        [HttpGet]
        [Route("api/orders/verify")]
        public async Task<bool> VerifyOrderBool(ulong? orderId, string dateFrom = null, string dateTo = null)
        {
            return await VerifyOrderBool(new OrderRouteModel(orderId, dateFrom, dateTo));
        }

        private async Task<bool> VerifyOrderBool(OrderRouteModel orderRoute)
        {
            var order = (await Get(orderRoute)).FirstOrDefault();
            return await VerifyOrderBool(order);
        }

        [HttpGet]
        //[Route("api/orders/invoiceaccrual")]
        public async Task<ActionResult> GetInvoiceAccrual(ulong? orderId = null, WcOrder order = null)
        {
            if (orderId == null && order == null)
            {
                return new EmptyResult();
            }
            try
            {
                var invoice = await Verify(order);
                return View("Partial/InvoiceAccrual", invoice);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return new EmptyResult();
        }

        private async Task<bool> VerifyOrderBool(WcOrder order)
        {
            try
            {
                return await Verify(order) != null;
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return false;
            }
        }

        private async Task<List<WcOrder>> Get(OrderRouteModel orderRoute)
        {
            var orders = new List<WcOrder>();

            try
            {
                if (orderRoute.HasDateRange())
                {
                    orders = await _wcOrderApi.GetOrders(orderRoute.DateFrom, orderRoute.DateTo, _memoryCache);
                }
                else
                {
                    orders.Add(await _wcOrderApi.Get(orderRoute.OrderId));
                }
            }
            catch (Exception ex)
            {
                // TODO: 
                // Json Parse:
                /* {
                    "code":"woocommerce_rest_shop_order_invalid_id",
                    "message":"Invalid ID.",
                    "data":{ "status":404 }
                    }
                */
                // And use switch with "message" content

                ViewBag.Error = "Woo Commerce Error, Message: " +
                ex.Message.Contains("Invalid ID.") switch
                {
                    true => $"Invalid Order ID: {orderRoute.OrderId}",
                    _ => ex.Message
                };
            }
            return orders;
        }

        public async Task<IActionResult> Orders(string dateFrom = null, string dateTo = null)
        {
            var orderRoute = new OrderRouteModel(null, dateFrom, dateTo);
            if (!orderRoute.IsValid())
            {
                return View("Orders");
            }
            var orders = await Get(orderRoute);
            var errors = new Dictionary<ulong?, string>();
            ulong? lastFailedOrderId = null;

            var invoices = orders
                .ToDictionary(o => o.id, o =>
                {
                    try
                    {
                        return Verify(o).Result;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(o.id, ex.Message);
                        lastFailedOrderId = o.id;
                        return null;
                    }
                });

            ViewBag.Message = errors.Count switch
            {
                0 => ViewBag.Message ?? (orders.Count == 1) ? $"Beställningen är Verifierad" : $"Alla {orders.Count} Beställningar är Verifierade.",
                1 => ViewBag.Message = $"Ett Verifikat misslyckades, Order Id: {GenOrderActionLinkHTML(lastFailedOrderId)}<br>{errors.First().Value}",
                _ => ViewBag.Message = $"{errors.Count} st av {orders.Count} totalt Verifikat misslyckades."
            };

            _orderViewModel = new OrderViewModel(orders, orderRoute, invoices, errors);

            if (errors.Count == 0)
            {
                _orderViewModel.Invoice = invoices.ConcatInvoices().TrySymplify(sort: true);

                _orderViewModel.TotalDebit = _orderViewModel.Invoice.InvoiceAccrualRows.GetTotalDebit();
                _orderViewModel.TotalCredit = _orderViewModel.Invoice.InvoiceAccrualRows.GetTotalCredit();
            }

            return View("Orders", _orderViewModel);
        }

        private static string GenOrderActionLinkHTML(ulong? orderId)
        {
            return $"<a href=\"/Verification?orderId={orderId}\">{orderId}</a>";
        }

        private async Task<decimal> Sum(List<WcOrder> orders, bool EUR = false)
        {
            decimal result = 0M;
            foreach (var order in orders)
            {
                var total = order.GetAccurateTotal();
                var currencyRate = EUR ? 1.0M : await GetCurrencyRate(order, total);
                var shipping = (decimal)order.shipping_total;
                result += (total + shipping) * currencyRate;
            }
            return result;
        }

        [HttpGet]
        public async Task<decimal> Sum(string dateFrom = null, string dateTo = null, bool EUR = false)
        {
            if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
                return 0;
            var orderRoute = new OrderRouteModel(null, dateFrom, dateTo);
            var orders = await Get(orderRoute);
            return await Sum(orders);
        }

        public async Task<ActionResult> Summation(string dateFrom = null, string dateTo = null, bool EUR = false)
        {
            if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
            {
                dateFrom = $"{DateTime.Now.AddDays(-1):yyyy-MM-dd}";
                dateTo = $"{DateTime.Now:yyyy-MM-dd}";
                return RedirectToAction("Summationa", new { dateFrom, dateTo });
            }
            var total = await Sum(dateFrom, dateTo, EUR);
            ViewData["TotalSEK"] = $"{total:0.00}";
            return View("Summation");
        }

        private async Task<InvoiceAccrual> Verify(WcOrder order, bool simplify = true)
        {
            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            return VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, simplify: simplify, coupons: _coupons);
        }

        public async Task<string> VerifyDates(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            var orders = await Get(orderRoute);

            double largestDiffInHours = 0.0;

            var diffDayCount = new Dictionary<double, int>();


            orders.ForEach(o =>
            {
                double diff = ((DateTime)(o.date_paid ?? o.date_completed) - (DateTime)o.date_created).TotalHours;
                if (diff > largestDiffInHours) largestDiffInHours = diff;
                var diffDays = Math.Floor(diff / 24.0);

                diffDayCount.TryGetValue(diffDays, out int count);
                diffDayCount[diffDays] = count + 1;
            });
            return "";
        }

        public async Task<ActionResult> Verification(ulong? orderId = null, string dateFrom = null, string dateTo = null, bool simplify = true)
        {
            /* if (_wcOrderApi == null) { return RedirectToAction("Index"); } */
            if (orderId == null && (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo)))
            {
                dateFrom = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
                dateTo = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
            }
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            var orders = await Get(orderRoute);
            ViewData["TotalDebitForPeriod"] = $"{await Sum(orders):0.00}";
            return await VerifyOrder(orders, orderRoute, simplify);
        }
    }
}