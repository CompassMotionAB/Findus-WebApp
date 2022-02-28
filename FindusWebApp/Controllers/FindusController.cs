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

namespace FindusWebApp.Controllers
{
    public class VerificationResult
    {
        public InvoiceAccrual InvoiceAccrual;
        public Invoice Invoice;

        public string ErrorMessage;

        public VerificationResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public VerificationResult(InvoiceAccrual invoiceAccrual, Invoice invoice, string errorMessage = null)
        {
            InvoiceAccrual = invoiceAccrual;
            Invoice = invoice;
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
            return await Verification(orderId, dateFrom, dateTo);
            /*
            var orderRoute = new OrderRouteModel(null, dateFrom, dateTo);
            var orders = await Get(orderRoute);
            //ViewData["Orders"] = orders;
            //ViewData["OrderValidation"] = orders.ToDictionary(order => order.id, async order => await VerifyOrderBool(order));
            _orderViewModel = new OrderViewModel(orders, orderRoute);
            return View("Findus", _orderViewModel);
            */
        }

        private async Task<decimal> GetCurrencyRate(WcOrder order, decimal? accurateTotal = null)
        {
            if (VerificationUtils.GetPaymentMethod(order) == "Stripe")
            {
                bool stripeCharge = (string)order.meta_data.First(d => d.key == "_stripe_charge_captured").value == "yes";
                decimal stripeFee = decimal.Parse((string)order.meta_data.First(d => d.key == "_stripe_fee").value);
                decimal stripeNet = decimal.Parse((string)order.meta_data.First(d => d.key == "_stripe_net").value);
                string stripeCurrency = (string)order.meta_data.First(d => d.key == "_stripe_currency").value;

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
                    decimal accurateTotal = order.GetAccurateTotal();
                    decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
                    var invoice = VerificationUtils.GenInvoice(order, currencyRate);
                    var invoiceAccrual = VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, simplify, coupons: _coupons);

                    result.Add((ulong)order.id, new VerificationResult(invoiceAccrual, invoice));
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

                decimal accurateTotal = order.GetAccurateTotal();
                decimal currencyRate = await GetCurrencyRate(order, accurateTotal);

                var invoice = VerificationUtils.GenInvoice(order, currencyRate);
                var invoiceAccrual = VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, simplify, coupons: _coupons);

                TempData["Invoice"] = invoice;
                TempData["InvoiceAccrual"] = invoiceAccrual;
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

        private async Task<bool> VerifyOrderBool(WcOrder order)
        {
            Fortnox.SDK.Entities.InvoiceAccrual invoiceAccrual;
            Fortnox.SDK.Entities.Invoice invoice;

            try
            {
                decimal accurateTotal = order.GetAccurateTotal();
                decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
                invoice = VerificationUtils.GenInvoice(order, currencyRate);
                invoiceAccrual = VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, coupons: _coupons);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return false;
            }

            return invoice != null && invoiceAccrual != null;
        }

        public async Task<ActionResult> Order(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            if (orderRoute.IsValid())
            {
                var orders = await Get(orderRoute);
                _orderViewModel = new OrderViewModel(orders, orderRoute);
            }

            return View("Findus", _orderViewModel);
        }

        private async Task<List<WcOrder>> Get(OrderRouteModel orderRoute)
        {
            var orders = new List<WcOrder>();
            if (orderRoute.HasDateRange())
            {
                return await _wcOrderApi.GetOrders(orderRoute.DateFrom, orderRoute.DateTo, _memoryCache);
            }
            try
            {
                orders.Add(await _wcOrderApi.Get(orderRoute.OrderId));
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

        public async Task<IActionResult> VerificationBatch(string dateFrom = null, string dateTo = null)
        {
            var orderRoute = new OrderRouteModel(null, dateFrom, dateTo);
            try
            {
                ViewData["VerificationData"] = await VerifyOrders(orderRoute);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View("Findus");
        }

        public async Task<ActionResult> Verification(ulong? orderId = null, string dateFrom = null, string dateTo = null, bool simplify = true)
        {
            /* if (_wcOrderApi == null) { return RedirectToAction("Index"); } */
            if(orderId == null && (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))) {
                dateFrom = $"{DateTime.Now.AddDays(-8):yyyy-MM-dd}";
                dateTo = $"{DateTime.Now.AddDays(-1).EndOfDay():yyyy-MM-dd}";
            }
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            var orders = await Get(orderRoute);
            return await VerifyOrder(orders, orderRoute, simplify);
        }
    }
}