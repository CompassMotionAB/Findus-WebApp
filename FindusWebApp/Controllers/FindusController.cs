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
using FindusWebApp.Services.Fortnox;
using Fortnox.SDK.Exceptions;
using Fortnox.SDK.Search;
using Customer = Fortnox.SDK.Entities.Customer;

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
        private readonly MemoryCacheEntryOptions _cacheEntryOptions;
        private readonly AccountsModel _accounts;
        private readonly dynamic _coupons;
        private OrderViewModel _orderViewModel;
        private readonly IFortnoxServices _fortnox;

        public FindusController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IOptions<WooKeys> wcKeysOptions, IFortnoxServices fortnox)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _cacheEntryOptions = new MemoryCacheEntryOptions()
                                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));
            _fortnox = fortnox;

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

        public ActionResult Index(ulong? orderId = null, string dateFrom = null, string dateTo = null)
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

        private async Task<ActionResult> VerifyOrder(List<WcOrder> orders, OrderRouteModel orderRoute, bool simplify = true)
        {
            try
            {
                _orderViewModel = new OrderViewModel(orders, orderRoute);
                WcOrder order = _orderViewModel.GetOrder();
                if (order.status != "completed")
                    throw new Exception($"Unexpected order status: {order.status}");

                //TempData["InvoiceAccrual"] = await GenInvoiceAccrual(order, simplify);
                GenInvoices(order, simplify);
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
            if (orderId != null || order != null)
            {
                try
                {
                    order = order != null ? await Get(orderId) : order;
                    var invoice = await GenInvoiceAccrual(order);
                    return View("Partial/InvoiceAccrual", invoice);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                }
            }
            return new EmptyResult();
        }
        public async Task<ActionResult> GetInvoice(ulong? orderId = null, WcOrder order = null)
        {
            if (orderId != null || order != null)
            {
                try
                {
                    order = order != null ? await Get(orderId) : order;
                    var invoice = await VerifyInvoice(order);
                    return View("DisplayTemplates/Invoice", invoice);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                }
            }
            return new EmptyResult();
        }

        public async Task<Invoice> VerifyInvoice(WcOrder order, decimal? currencyRate = null)
        {
            currencyRate ??= await GetCurrencyRate(order);
            return VerificationUtils.GenInvoice(order, (decimal)currencyRate);
        }

        private async Task<bool> VerifyOrderBool(WcOrder order)
        {
            try
            {
                return await GenInvoiceAccrual(order) != null;
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return false;
            }
        }

        private async Task<WcOrder> Get(ulong? orderId, string orderStatus = "completed")
        {
            return await _wcOrderApi.GetOrder(orderId, orderStatus);
        }
        private async Task<List<WcOrder>> Get(OrderRouteModel orderRoute)
        {
            var orders = new List<WcOrder>();

            try
            {
                if (orderRoute.HasDateRange())
                {
                    orders = await _wcOrderApi.GetOrders(orderRoute.DateFrom, orderRoute.DateTo, _memoryCache, orderStatus: orderRoute.Status);
                }
                else
                {
                    orders.Add(await _wcOrderApi.GetOrder(orderRoute));
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

        public async Task<IActionResult> Orders(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
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
                        return GenInvoiceAccrual(o).Result;
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

        private async Task<InvoiceAccrual> GenInvoiceAccrual(WcOrder order, bool simplify = true)
        {
            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            return VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, simplify: simplify, coupons: _coupons);
        }
        private async void GenInvoices(WcOrder order, bool simplify = true)
        {
            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            TempData["InvoiceAccrual"] = VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, simplify: simplify, coupons: _coupons);
            TempData["Invoice"] = VerificationUtils.GenInvoice(order, currencyRate, accurateTotal);
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
            if (orderId == null && (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo)))
            {
                dateFrom = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
                dateTo = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
            }
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo, "completed");
            var orders = await Get(orderRoute);
            //ViewData["TotalDebitForPeriod"] = $"{await Sum(orders):0.00}";
            return await VerifyOrder(orders, orderRoute, simplify);
        }

        [HttpGet]
        public async Task<IActionResult> SendToFortnox(ulong? orderId = null, string dateFrom = null, string dateTo = null)
        {
            try
            {
                var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
                var orders = await Get(orderRoute);
                foreach (var order in orders)
                {
                    if (await HasOrder(order.id))
                    {
                        ViewBag.Error = $"Order Id: {order.id} already exists in Fortnox.";
                        return RedirectToAction("Orders", new { order.id, dateFrom, dateTo });
                    }

                    decimal currencyRate = await GetCurrencyRate(order);
                    VerificationModel verification = VerificationUtils.Verify(order, _accounts, currencyRate, coupons: _coupons);
                    if (string.IsNullOrEmpty(verification.Error) && !await SendToFortnox(verification))
                    {
                        orderRoute.OrderId = order.id;
                        ViewBag.Error ??= (verification.Error ?? "Failed to verify order.");
                        var errors = new Dictionary<ulong?, string> { { order.id, ViewBag.Error } };
                        return View("Orders", new OrderViewModel(orders, orderRoute, errors: errors));
                    }
                    else
                    {
                        //return await Orders(orderId, dateFrom, dateTo);
                        ViewBag.Error ??= $"Failed to verify order id: {order.id}";
                        return RedirectToAction("Orders", new { order.id, dateFrom, dateTo });
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View("Findus");
        }

        private async Task<bool> SendToFortnox(VerificationModel verification)
        {
            if (!verification.IsValid())
            {
                ViewBag.Error = verification.Error ?? ViewBag.Error;
                return false;
            }
            try
            {
                TempData["Customer"] = verification.Customer;
                //await Call(UpdateCustomer);
                try
                {
                    await _fortnox.FortnoxApiCall(UpdateCustomer);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    return false;
                }
                var customerNr = TempData["CustomerNr"] as string;
                if (ViewBag.Error != null)
                {
                    if (string.IsNullOrEmpty(customerNr))
                    {
                        throw new Exception($"Failed to update customer: {verification.Customer.Email} for order id: {verification.OrderId}.");
                    }
                    return false;
                }
                TempData["OrderItems"] = verification.OrderItems;
                await Call(UpdateArticles);

                verification.Invoice.CustomerNumber = customerNr;
                TempData["Invoice"] = verification.Invoice;
                TempData["InvoiceAccrual"] = verification.InvoiceAccrual;
                try
                {
                    await Call(CreateFortnoxInvoices);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    return false;
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return false;
            }
            return true;
        }
        /*
        private async Task<IActionResult> SendToFortnox(WcOrder order)
        {
            //await Call(DEBUGCreateFinancialYear);

            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            var invoice = VerificationUtils.GenInvoice(order, currencyRate);
            TempData["Customer"] = invoice.GetCustomer(order);
            //await Call(UpdateCustomer);
            await _fortnox.FortnoxApiCall(UpdateCustomer);
            var customerNr = TempData["CustomerNr"] as string;
            if (String.IsNullOrEmpty(customerNr)) throw new Exception($"Failed to update customer: {order.billing.email} for order id: {order.id}.");

            TempData["LineItems"] = order.line_items;
            await Call(UpdateArticles);

            invoice.CustomerNumber = customerNr;
            TempData["Invoice"] = invoice;
            TempData["InvoiceAccrual"] = VerificationUtils.GenInvoiceAccrual(order, _accounts, currencyRate, accurateTotal, coupons: _coupons, customerNr: customerNr);
            await Call(CreateFortnoxInvoices);
            return View("Findus");
        }*/

        private async void DEBUGCreateFinancialYear(FortnoxContext context, int year = 2022)
        {
            var FromDate = new DateTime(year, 1, 1);
            var ToDate = new DateTime(year + 1, 1, 1).AddTicks(-1);
            try
            {
                var yearSubsetList = await context.Client.FinancialYearConnector.FindAsync(
                    new FinancialYearSearch
                    {
                        Date = FromDate,
                    });
            }
            catch (Exception ex)
            {
                await context.Client.FinancialYearConnector.CreateAsync(new FinancialYear
                {
                    FromDate = FromDate,
                    ToDate = ToDate,
                    AccountingMethod = AccountingMethod.Accrual,
                    AccountChartType = $"BAS {year}"
                });
            }
        }

        private async Task Call(Action<FortnoxContext> action)
        {
            try
            {
                await _fortnox.FortnoxApiCall(action);
            }
            catch (FortnoxApiException ex)
            {
                ViewBag.Error = ex.Message;
            }
        }

        private async void TryGetCustomer(FortnoxContext context)
        {
            var customerEmail = TempData.Peek("CustomerEmail") as string;
            try
            {
                var customerCon = context.Client.CustomerConnector;
                var customerSubsetList = await customerCon.FindAsync(new CustomerSearch() { Email = customerEmail, });

                if (customerSubsetList?.Entities.Count == 1)
                {
                    TempData["CustomerNr"] = customerSubsetList.Entities.FirstOrDefault().CustomerNumber;
                    return;
                }
                else if (customerSubsetList?.Entities.Count > 1)
                {
                    throw new Exception("Flera kunder med samma Email existerar i Fortnox.");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            TempData["CustomerNr"] = null;
        }
        private async Task<string> TryGetCustomerNr(string customerEmail)
        {
            TempData["CustomerEmail"] = customerEmail;
            await Call(TryGetCustomer);
            return TempData.Peek("CustomerNr") as string;
        }

        private async Task<bool> HasOrder(ulong? orderId)
        {
            TempData["OrderId"] = orderId;
            await Call(TryGetOrder);
            return TempData.Peek("HasOrder") != null;// && (bool)TempData["HasOrder"];
        }
        private async void TryGetOrder(FortnoxContext context)
        {
            if (TempData["OrderId"] is not ulong orderId) throw new Exception("OrderId is not defined.");
            try
            {
                var orderCollection = await context.Client.InvoiceConnector.FindAsync(new InvoiceSearch
                {
                    YourOrderNumber = orderId.ToString()
                });
                TempData["HasOrder"] = orderCollection?.Entities.Count > 0;
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
        }

        private async void UpdateCustomer(FortnoxContext context)
        {
            if (TempData["Customer"] is not Customer customer) throw new Exception("Customer is not defined.");
            var customerNr = await TryGetCustomerNr(customer.Email);

            var client = context.Client;
            var customerConn = client.CustomerConnector;

            if (customerNr != null)
            {
                customer.CustomerNumber = customerNr;
                try
                {
                    await customerConn.UpdateAsync(customer);
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    //throw new Exception($"Failed to update Customer: {customer.Email}\n{ex.Message}");
                    customerNr = null;
                }
            }
            else
            {
                try
                {
                    var newCustomer = await customerConn.CreateAsync(customer);
                    customerNr = newCustomer.CustomerNumber;
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    //throw new Exception($"Failed to create new Customer: {customer.Email}\n{ex.Message}");
                    customerNr = null;
                }
            }
            TempData["CustomerNr"] = customerNr;
        }

        private async void UpdateArticles(FortnoxContext context)
        {
            if (TempData["OrderItems"] is not List<OrderLineItem> items) throw new Exception("OrderItems is not defined.");
            var articleCon = context.Client.ArticleConnector;
            foreach (var item in items)
            {
                var product = await articleCon.FindAsync(new ArticleSearch { ArticleNumber = item.sku });
                if (product?.Entities.Count == 0)
                {
                    await articleCon.CreateAsync(new Article
                    {
                        ArticleNumber = item.sku,
                        Type = ArticleType.Stock,
                        Description = item.name.Replace('|', '-')
                    });
                }
            }
        }

        private async void CreateFortnoxInvoices(FortnoxContext context)
        {
            if (TempData["Invoice"] is not Invoice invoice) throw new Exception("Invoice is not defined.");
            if (TempData["InvoiceAccrual"] is not InvoiceAccrual invoiceAccrual) throw new Exception("InvoiceAccrual is not defined.");

            var invoiceCon = context.Client.InvoiceConnector;
            try {
            if ((await invoiceCon.FindAsync(new InvoiceSearch { YourOrderNumber = invoice.YourOrderNumber }))?.Entities.Count != 0)
                throw new Exception($"Faktura för order id: {invoice.YourOrderNumber} finns redan i Fortnox");
            } catch (Exception ex) {
                //throw new Exception($"Failed to search for invoice for Order Id:{invoice.YourOrderNumber}");
                ViewBag.Error = ex?.InnerException.Message ?? ex.Message;
                return;
            }
            invoice = await invoiceCon.CreateAsync(invoice);

            var invoiceAccCon = context.Client.InvoiceAccrualConnector;
            invoiceAccrual.InvoiceNumber = invoice.DocumentNumber;
            await invoiceAccCon.CreateAsync(invoiceAccrual);
        }
    }
}