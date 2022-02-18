using Customer = Fortnox.SDK.Entities.Customer;
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
using FindusWebApp.Helper;
using Microsoft.Extensions.Options;

namespace FindusWebApp.Controllers
{
    public class FindusController : Controller
    {

        private readonly WooKeys _wcKeys;
        private readonly WCObject.WCOrderItem _wcOrderApi;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheEntryOptions _orderCacheOptions;
        private OrderViewModel _orderViewModel;

        public FindusController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<WooKeys> wcKeysOptions)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _orderCacheOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromHours(8));

            _wcKeys = wcKeysOptions.Value;

            if(string.IsNullOrEmpty(_wcKeys.Key) || string.IsNullOrEmpty(_wcKeys.Secret) || string.IsNullOrEmpty(_wcKeys.Url)) {
                ViewBag.Error = "Missing WooKeys Configuration, see appsettings.sample.json";
            } else {
                RestAPI restJwt = new RestAPI(_wcKeys.Url, _wcKeys.Key, _wcKeys.Secret, false);
                _wcOrderApi = new WCObject.WCOrderItem(restJwt);
            }

            ViewBag.CultureInfo = new System.Globalization.CultureInfo("sv-SE");
        }

        public async Task<ActionResult> Index(string dateFrom = null, string dateTo = null)
        {
            if(string.IsNullOrEmpty(_wcKeys.Key) || string.IsNullOrEmpty(_wcKeys.Secret) || string.IsNullOrEmpty(_wcKeys.Url)) {
                ViewBag.Error = "Missing WooKeys Configuration, see appsettings.sample.json";
                return View("Findus");
            }
            var orders = await FetchOrdersAsync(dateFrom, dateTo);
            _orderViewModel = new OrderViewModel(orders);
            return View("Findus", _orderViewModel);
        }

        private async Task<WcOrder> FetchOrderAsync(ulong orderId)
        {
            return await _wcOrderApi.Get((int)orderId);
        }
        private async Task<List<WcOrder>> FetchOrdersAsync(string dateFrom = null, string dateTo = null)
        {
            bool noFrom = string.IsNullOrEmpty(dateFrom);
            bool noTo = string.IsNullOrEmpty(dateTo);
            if (noFrom != noTo)
            {
                throw new ArgumentException($"Expected dateFrom and dateTo to be either null or both defined. Received: dateFrom: {dateFrom}, dateTo:{dateTo}");
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
            var cacheKey = $"{dateAfter:yyyy-MM-dd}_{dateBefore:yyyy-MM-dd}-orders";

            if (!_memoryCache.TryGetValue(cacheKey, out List<WcOrder> orders))
            {
                orders = await _wcOrderApi.GetAll(new Dictionary<string, string>() {
                    {"page", "1"},
                    {"per_page", "16"},
                    {"after", $"{dateAfter:yyyy-MM-ddTHH:mm:ss}"},
                    {"before", $"{dateBefore:yyyy-MM-ddTHH:mm:ss}"},
                    {"status", "completed"}
                });

                _memoryCache.Set(cacheKey, orders, _orderCacheOptions);
            }
            return orders;
        }

        private async Task<decimal> GetCurrencyRate(WcOrder order, decimal? accurateTotal = null)
        {
            if (VerificationUtils.GetPaymentMethod(order) == "Stripe")
            {
                bool stripeCharge = (string)order.meta_data.First(d => d.key == "_stripe_charge_captured").value == "yes";
                decimal stripeFee = decimal.Parse((string)order.meta_data.First(d => d.key == "_stripe_fee").value);
                decimal stripeNet = decimal.Parse((string)order.meta_data.First(d => d.key == "_stripe_net").value);
                string stripeCurrency = (string)order.meta_data.First(d => d.key == "_stripe_currency").value;

                if(!stripeCharge) {
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

        public async Task<ActionResult> Verification(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            if(_wcOrderApi == null) {
                return RedirectToAction("Index");
            }
            if (_orderViewModel == null)
            {
                var orders = await FetchOrdersAsync(dateFrom, dateTo);
                _orderViewModel = new OrderViewModel(orders, orderId);
            }
            WcOrder order = _orderViewModel.GetOrder();

            var accounts = new AccountsModel(
                Utilities.LoadJson<Dictionary<string, AccountModel>>("VATAccounts.json"),
                Utilities.LoadJson<Dictionary<string, AccountModel>>("SalesAccounts.json")
            );
            try
            {
                decimal accurateTotal = order.GetAccurateTotal();

                decimal currencyRate = await GetCurrencyRate(order, accurateTotal);

                var inv = VerificationUtils.GenInvoice(order, currencyRate);
                var invAccrual = VerificationUtils.GenInvoiceAccrual(order, accounts, currencyRate, accurateTotal);

                TempData["invoice"] = inv;
                TempData["invoiceAccrual"] = invAccrual;
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }

            return View("Findus", _orderViewModel);
        }
    }
}