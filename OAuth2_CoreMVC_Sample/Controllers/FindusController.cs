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

namespace Findus.Controllers
{
    public class FindusController : Controller
    {
        private readonly WCObject.WCOrderItem _wcOrderApi;
        private readonly IHttpClientFactory _httpClientFactory;

        public FindusController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;

            const string key = "ck_72fb56479746009c1dd4f2eebffecdba922c2f61";
            const string secret = "cs_1d15538d3e5be3fb5ba17be4f736902e5ff86348";

            RestAPI restJwt = new RestAPI("https://gamerbulk.com/wp-json/wc/v2", key, secret, false);

            _wcOrderApi = new WCObject.WCOrderItem(restJwt);

            ViewBag.CultureInfo = new System.Globalization.CultureInfo("en-us");
        }

        public async Task<ActionResult> Index() {
            TempData["order"] = (await FetchOrdersAsync()).FirstOrDefault();
            return View("Findus");
        }

        private async Task<List<WcOrder>> FetchOrdersAsync(string dateFrom = null, string dateTo = null)
        {
            bool noFrom = string.IsNullOrEmpty(dateFrom);
            bool noTo = string.IsNullOrEmpty(dateTo);
            if (noFrom != noTo) {
                throw new ArgumentException($"Expected dateFrom and dateTo to be either null or both defined. Received: dateFrom: {dateFrom}, dateTo:{dateTo}");
            }

            if (noFrom && noTo)
            {
                dateFrom = Utilities.DateString(DateTime.Now.AddDays(-7));
                dateTo = Utilities.DateString(DateTime.Now);
            } else {
                dateFrom = Utilities.DateString(dateFrom);
                dateTo = Utilities.DateString(DateTime.Parse(dateTo).AddDays(1).AddTicks(-1)); // End of Day
            }

            return await _wcOrderApi.GetAll(new Dictionary<string, string>() {
                    {"page", "1"},
                    {"per_page", "1"},
                    {"after", dateFrom},
                    {"before", dateTo},
                    {"status", "completed"}
                });
        }

        public async Task<ActionResult> Verification(){
            TempData["order"] = FetchOrdersAsync().Result.FirstOrDefault();

            if (!(TempData["order"] is WcOrder order)) throw new Exception();

            DateTime date = (DateTime)order.date_paid;
            var httpClient = _httpClientFactory.CreateClient();
            decimal currencyRate = await CurrencyUtils.GetSEKCurrencyRateAsync(date, order.currency.ToUpper(), httpClient);

            var accounts = new AccountsModel(
                Utilities.LoadJson<Dictionary<string, AccountModel>>("VATAccounts.json"),
                Utilities.LoadJson<Dictionary<string, AccountModel>>("SalesAccounts.json")
            );

            var inv = VerificationUtils.GenInvoice(order, currencyRate);
            var invAccrual = VerificationUtils.GenInvoiceAccrual(order, accounts, currencyRate);

            TempData["invoice"] = inv;
            TempData["invoiceAccrual"] = invAccrual;
            return View("Findus");
        }
    }
}