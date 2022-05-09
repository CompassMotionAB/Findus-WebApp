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
using FindusWebApp.Models;
using Microsoft.Extensions.Caching.Memory;
using FindusWebApp.Helpers;
using Microsoft.Extensions.Options;
using Fortnox.SDK.Entities;
using FindusWebApp.Extensions;
using FindusWebApp.Services.Fortnox;
using Fortnox.SDK.Exceptions;
using Fortnox.SDK.Search;
using Customer = Fortnox.SDK.Entities.Customer;
using System.Threading;
using System.Net;
using Fortnox.SDK.Interfaces;

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
        private OrderViewModel _orderViewModel;
        private readonly IFortnoxServices _fortnox;

        public FindusController(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            IOptions<WooKeys> wcKeysOptions,
            IFortnoxServices fortnox
        )
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(
                TimeSpan.FromMinutes(30)
            );
            _fortnox = fortnox;

            _wcKeys = wcKeysOptions.Value;

            if (
                string.IsNullOrEmpty(_wcKeys.Key)
                || string.IsNullOrEmpty(_wcKeys.Secret)
                || string.IsNullOrEmpty(_wcKeys.Url)
            )
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

            ViewBag.CultureInfo = new System.Globalization.CultureInfo("sv-SE");
        }

        public Task<IActionResult> IndexAsync()
        {
            if (
                string.IsNullOrEmpty(_wcKeys.Key)
                || string.IsNullOrEmpty(_wcKeys.Secret)
                || string.IsNullOrEmpty(_wcKeys.Url)
            )
            {
                ViewBag.Error = "Missing WooKeys Configuration, see appsettings.sample.json";
                // return View("Findus");
            }
            return CallRedirect(FetchCompanyName, "Findus/Orders");
            // return RedirectToAction("Verification", new { orderId, dateFrom, dateTo });
        }

        private async Task<decimal> GetCurrencyRate(WcOrder order, decimal? accurateTotal = null)
        {
            if (VerificationUtils.GetPaymentMethod(order) == "Stripe")
            {
                bool stripeCharge =
                    (string)order.meta_data.Find(d => d.key == "_stripe_charge_captured").value
                    == "yes";
                decimal stripeFee = decimal.Parse(
                    (string)order.meta_data.Find(d => d.key == "_stripe_fee").value
                );
                decimal stripeNet = decimal.Parse(
                    (string)order.meta_data.Find(d => d.key == "_stripe_net").value
                );
                string stripeCurrency = (string)order.meta_data
                    .Find(d => d.key == "_stripe_currency")
                    .value;

                if (!stripeCharge)
                {
                    throw new Exception("Unexpected: _stripe_charge_captured == false");
                }
                if (stripeCurrency != "SEK")
                {
                    throw new Exception(
                        $"Stripe Payment with currency: {stripeCurrency} is unsupported"
                    );
                }

                if (stripeFee <= 0.0M || stripeNet <= 0.0M)
                {
                    throw new Exception(
                        $"Stripe Fee or Net is empty or invalid, Fee: {stripeFee}, Net: {stripeNet}"
                    );
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
            decimal result = await CurrencyUtils.GetSEKCurrencyRateAsync(
                date,
                order.currency.ToUpper(),
                httpClient
            );
            httpClient.Dispose();
            return result;
        }

        private ActionResult VerifyOrder(
            List<WcOrder> orders,
            OrderRouteModel orderRoute,
            bool simplify = true
        )
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
        public async Task<bool> VerifyOrderBool(
            string orderId,
            string dateFrom = null,
            string dateTo = null
        )
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
        public async Task<ActionResult> GetInvoiceAccrual(
            string orderId = null,
            WcOrder order = null
        )
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

        public async Task<ActionResult> GetInvoice(string orderId = null, WcOrder order = null)
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
            return VerificationUtils.GenInvoice(order, (decimal)currencyRate, _accounts);
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

        private async Task<WcOrder> Get(string orderId, string orderStatus = "completed")
        {
            return await _wcOrderApi.GetOrder(orderId, orderStatus, _memoryCache);
        }

        private async Task<List<WcOrder>> Get(OrderRouteModel orderRoute)
        {
            var orders = new List<WcOrder>();

            try
            {
                if (orderRoute.HasDateRange())
                {
                    orders = await _wcOrderApi.GetOrders(
                        orderRoute.DateFrom,
                        orderRoute.DateTo,
                        _memoryCache,
                        orderStatus: orderRoute.Status
                    );
                }
                else
                {
                    orders.Add(await _wcOrderApi.GetOrder(orderRoute, _memoryCache));
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

                ViewBag.Error =
                    "Woo Commerce Error, Message: "
                    + (
                        ex.Message.Contains("Invalid ID.")
                          ? $"Invalid Order ID: {orderRoute.OrderId}"
                          : ex.Message
                    );
            }
            // Let's sort orders from newest to latest so that
            // invoice & customer number ascends the same way
            // as order id
            return orders.OrderBy(o => o.id).ToList();
        }

        private async Task<Invoice> CreatePartialRefund(WcOrder order, Invoice invoice)
        {
            var creditInvoice = await AddCreditInvoiceToInvoice(invoice.DocumentNumber);

            var updateInvoice = new Invoice()
            {
                DocumentNumber = creditInvoice.DocumentNumber
            }.SetInvoiceRows(order, _accounts);

            return await UpdateInvoice(updateInvoice);
        }

        private async Task<Invoice> CreateFullRefund(WcOrder order, long? invoiceRef = null)
        {
            // Create new Credit Invoice to existing Invoice in Fortnox
            return await AddCreditInvoiceToInvoice(invoiceRef ?? order.GetInvoiceReference());
        }

        private async Task<long?> GetInvoiceReference(WcOrder order)
        {
            long? invoiceRef;
            // TODO: This catch scope shouldn't be needed in production
            // since GetInvoiceReference should work then.
            try
            {
                invoiceRef = order.GetInvoiceReference();
            }
            catch
            {
                var tmpInvoice = await GetInvoiceFromOrderId(order.id.ToString());
                invoiceRef = tmpInvoice?.DocumentNumber;
            }
            return invoiceRef;
        }

        public async Task<IActionResult> Refunds(
            string dateFrom = null,
            string dateTo = null,
            bool applyRefunds = false
        )
        {
            var orderRoute = new OrderRouteModel(orderId: null, dateFrom, dateTo, "refunded");
            if (orderRoute.IsValid())
            {
                var orders = await Get(orderRoute);
                var invoices = new Dictionary<string, Invoice>();
                var errors = new Dictionary<string, string>();
                var warnings = new Dictionary<string, string>();

                foreach (var order in orders)
                {
                    var orderId = order.id.ToString();

                    long? invoiceRef = await GetInvoiceReference(order);

                    if (!invoiceRef.HasValue)
                    {
                        errors.Add(orderId, "Order is missing reference to Fortnox Invoice.");
                    }
                    else
                    {
                        try
                        {
                            var invoice = await GetInvoiceFromReference(invoiceRef);

                            if(VerificationUtils.CanBeRefunded(order, invoice, ref errors))
                            {
                                if (applyRefunds)
                                {
                                    // Full refund
                                    var invoiceWithCredit = await CreateFullRefund(
                                        order,
                                        invoiceRef
                                    );

                                    // Add external reference to order ID
                                    // invoice.ExternalInvoiceReference2 = refundId;
                                    // await UpdateInvoice(invoice);

                                    /*
                                    await _wcOrderApi.AddInvoiceReferenceAsync(
                                        refundId,
                                        invoiceWithCredit.CreditInvoiceReference
                                    );
                                    */
                                }

                                invoices.Add(orderId, invoice);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(orderId, ex.InnerException?.Message ?? ex.Message);
                        }
                    }
                }

                // Get partially refunded orders

                var partialOrders = await _wcOrderApi.GetPartialRefundedOrders(
                    dateFrom,
                    dateTo,
                    _memoryCache
                );

                foreach (var order in partialOrders)
                {
                    var orderId = order.id.ToString();

                    long? invoiceRef = await GetInvoiceReference(order);

                    if (!invoiceRef.HasValue)
                    {
                        errors.Add(orderId, "Order is missing reference to Fortnox Invoice.");
                    }
                    else
                    {
                        try
                        {
                            var invoice = await GetInvoiceFromReference(invoiceRef);


                            if(VerificationUtils.CanBeRefunded(order, invoice, ref errors))
                            {
                                warnings.Add(orderId, "This is a Partial refund.");
                                if (applyRefunds)
                                {
                                    // Partial refund
                                    var invoiceWithCredit = await CreatePartialRefund(
                                        order,
                                        invoice
                                    );

                                    // Add external reference to order ID
                                    // invoice.ExternalInvoiceReference2 = refundId;
                                    // await UpdateInvoice(invoice);

                                    /*
                                    await _wcOrderApi.AddInvoiceReferenceAsync(
                                        refundId,
                                        invoiceWithCredit.CreditInvoiceReference
                                    );
                                    */
                                }

                                invoices.Add(orderId, invoice);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(orderId, ex.InnerException?.Message ?? ex.Message);
                        }
                    }

                    /*
                    var creditInvoiceRef = invoiceWithCredit.CreditInvoiceReference;

                    // Partial Refund
                    var creditInvoice = await GetInvoiceFromReference(creditInvoiceRef);
                    var updatedCreditInvoice = new Invoice().SetInvoiceRows(order, _accounts);
                    */
                }

                _orderViewModel = new OrderViewModel(
                    orders,
                    orderRoute,
                    errors: errors,
                    warnings: warnings
                );
            }
            return View("Orders", _orderViewModel);
        }

        public async Task<IActionResult> Orders(
            string orderId = null,
            string dateFrom = null,
            string dateTo = null,
            bool redirect = true
        )
        {
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            if (!orderRoute.IsValid())
            {
                return View("Orders");
            }
            else if (redirect)
            {
                if (orderRoute.HasDateRange())
                {
                    return await CallRedirect(
                        FetchCompanyName,
                        redirectUrl: $"Findus/Orders?dateFrom={dateFrom}&dateTo={dateTo}&redirect=false"
                    );
                }
                else if (orderRoute.IsValid())
                {
                    return await CallRedirect(
                        FetchCompanyName,
                        redirectUrl: $"Findus/Orders?orderId={orderId}&redirect=false"
                    );
                }
                else
                {
                    return await CallRedirect(
                        FetchCompanyName,
                        redirectUrl: "Findus/Orders?redirect=false"
                    );
                }
            }
            var orders = await Get(orderRoute);
            var errors = new Dictionary<string, string>();
            var warnings = new Dictionary<string, string>();
            string lastFailedOrderId = null;

            var invoices = new Dictionary<string, InvoiceAccrual>();
            foreach (var order in orders)
            {
                var id = order.id.ToString();
                var hasOrder = await FortnoxHasOrder(id);

                if (hasOrder)
                {
                    warnings.Add(id, "Order already exists on Fortnox.");
                }
                else if (!order.HasDocumentLink())
                {
                    warnings.Add(id, "Order is missing PDF document link.");
                }
                try
                {
                    invoices.Add(id, await GenInvoiceAccrual(order));
                }
                catch (Exception ex)
                {
                    errors.Add(id, ex.Message);
                    lastFailedOrderId = id;
                }
            }

            if (invoices.Count == 0)
            {
                ViewBag.Message =
                    "Unexpected Error Occurred, make sure you have a valid order id or date for order(s)";
                return View("Findus");
            }

            ViewBag.Message = errors.Count switch
            {
                0
                  => ViewBag.Message ?? (orders.Count == 1)
                      ? "Beställningen är Verifierad"
                      : $"Alla {orders.Count} Beställningar är Verifierade.",
                1
                  => ViewBag.Message =
                      $"Ett av {orders.Count} Verifikat misslyckades, Order Id: {GenOrderActionLinkHTML(lastFailedOrderId)}<br>{errors.First().Value}",
                _
                  => ViewBag.Message =
                      $"{errors.Count} st av {orders.Count} totalt Verifikat misslyckades."
            };

            _orderViewModel = new OrderViewModel(orders, orderRoute, invoices, errors, warnings);

            return View("Orders", _orderViewModel);
        }

        private static string GenOrderActionLinkHTML(string orderId)
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
        public async Task<decimal> Sum(string dateFrom = null, string dateTo = null)
        {
            if (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo))
                return 0;
            var orderRoute = new OrderRouteModel(null, dateFrom, dateTo);
            var orders = await Get(orderRoute);
            return await Sum(orders);
        }

        private async Task<InvoiceAccrual> GenInvoiceAccrual(WcOrder order, bool simplify = true)
        {
            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            return VerificationUtils.GenInvoiceAccrual(
                order,
                _accounts,
                currencyRate,
                accurateTotal,
                simplify: simplify
            );
        }

        private async void GenInvoices(WcOrder order, bool simplify = true)
        {
            decimal accurateTotal = order.GetAccurateTotal();
            decimal currencyRate = await GetCurrencyRate(order, accurateTotal);
            TempData["InvoiceAccrual"] = VerificationUtils.GenInvoiceAccrual(
                order,
                _accounts,
                currencyRate,
                accurateTotal,
                simplify: simplify
            );
            TempData["Invoice"] = VerificationUtils.GenInvoice(order, currencyRate, _accounts);
        }

        public async Task<string> DEBUG_VerifyDates(
            string orderId = null,
            string dateFrom = null,
            string dateTo = null
        )
        {
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
            var orders = await Get(orderRoute);

            double largestDiffInHours = 0.0;

            var diffDayCount = new Dictionary<double, int>();

            orders.ForEach(
                o =>
                {
                    double diff = (
                        (DateTime)(o.date_paid ?? o.date_completed) - (DateTime)o.date_created
                    ).TotalHours;
                    if (diff > largestDiffInHours)
                        largestDiffInHours = diff;
                    var diffDays = Math.Floor(diff / 24.0);

                    diffDayCount.TryGetValue(diffDays, out int count);
                    diffDayCount[diffDays] = count + 1;
                }
            );
            return "";
        }

        public async Task<ActionResult> Verification(
            string orderId = null,
            string dateFrom = null,
            string dateTo = null,
            bool simplify = true
        )
        {
            if (orderId == null && (string.IsNullOrEmpty(dateFrom) || string.IsNullOrEmpty(dateTo)))
            {
                dateFrom = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
                dateTo = $"{DateTime.Now.AddDays(0):yyyy-MM-dd}";
            }
            var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo, "completed");
            var orders = await Get(orderRoute);
            //ViewData["TotalDebitForPeriod"] = $"{await Sum(orders):0.00}";
            return VerifyOrder(orders, orderRoute, simplify);
        }

        private async void UploadPDFToFortnox(FortnoxContext context)
        {
            if (TempData["pdf"] is not byte[] pdf)
                throw new Exception("PDF data missing for upload.");
            if (TempData["invoiceNr"] is not long invoiceNr)
                throw new Exception("Document number missing for PDF upload.");
            if (TempData["orderId"] is not string orderId)
                throw new Exception("Order Id is missing for PDF upload.");

            var connector = context.Client.InvoiceFileConnectionConnector;
            var inboxConnector = context.Client.InboxConnector;
            var tmpFile = await inboxConnector.UploadFileAsync(
                $"invoice-{orderId}.pdf",
                pdf,
                StaticFolders.CustomerInvoices
            );
            var newInvoiceFileConnection = new InvoiceFileConnection()
            {
                EntityId = invoiceNr,
                FileId = tmpFile.ArchiveFileId,
                IncludeOnSend = false,
                EntityType = EntityType.Invoice
            };
            await connector.CreateAsync(newInvoiceFileConnection);
        }

        private async Task UploadPDF(byte[] pdf, long? invoiceNr, ulong orderId)
        {
            TempData["pdf"] = pdf;
            TempData["invoiceNr"] = invoiceNr;
            TempData["orderId"] = orderId.ToString();
            await Call(UploadPDFToFortnox);
        }

        private void FetchCompanyName(FortnoxContext context)
        {
            var client = context.Client;
            var conn = client.CompanyInformationConnector;
            ViewData["CompanyName"] = conn.GetAsync().Result.CompanyName;
        }

        public async Task<IActionResult> AddMissingPDF(string orderId = null)
        {
            if (string.IsNullOrEmpty(orderId))
                return new BadRequestResult();

            var order = await Get(orderId);
            if (!order.HasDocumentLink())
            {
                return new BadRequestResult();
            }
            var invoice = await GetInvoiceFromOrderId(orderId);
            if (invoice == null)
                return new BadRequestResult();

            var pdf = await FetchFile(order.TryGetDocumentLink());
            await UploadPDF(pdf, invoice.DocumentNumber, Convert.ToUInt64(orderId));
            return View("Findus");
        }

        [HttpGet]
        public async Task<IActionResult> SendToFortnox(
            string orderId = null,
            string dateFrom = null,
            string dateTo = null,
            bool redirect = true
        )
        {
            try
            {
                var errors = new Dictionary<string, string>();
                var warnings = new Dictionary<string, string>();
                var orderRoute = new OrderRouteModel(orderId, dateFrom, dateTo);
                if (redirect)
                {
                    try
                    {
                        await Call(FetchCompanyName);
                    }
                    catch
                    {
                        if (orderRoute.HasDateRange() && string.IsNullOrEmpty(orderRoute.OrderId))
                        {
                            return await CallRedirect(
                                FetchCompanyName,
                                redirectUrl: $"Findus/SendToFortnox?dateFrom={dateFrom}&dateTo={dateTo}&redirect=false"
                            );
                        }
                        else if (orderRoute.IsValid())
                        {
                            return await CallRedirect(
                                FetchCompanyName,
                                redirectUrl: $"Findus/SendToFortnox?orderId={orderId}&redirect=false"
                            );
                        }
                        else
                        {
                            return await CallRedirect(
                                FetchCompanyName,
                                redirectUrl: "Findus/SendToFortnox?redirect=false"
                            );
                        }
                    }
                }
                var orders = await Get(orderRoute);
                foreach (var order in orders)
                {
                    //var hasOrder = await FortnoxHasOrder(order.id.ToString());
                    var invoice = await GetInvoiceFromOrderId(order.id.ToString());
                    if (invoice != null)
                    {
                        warnings.Add(
                            order.id.ToString(),
                            $"Order {order.id}\nalready exists on Fortnox."
                        );
                    }
                    else
                    {
                        try
                        {
                            decimal currencyRate = await GetCurrencyRate(order);
                            VerificationModel verification = VerificationUtils.Verify(
                                order,
                                _accounts,
                                currencyRate
                            );
                            if (verification.IsValid())
                            {
                                try
                                {
                                    ViewBag.Error = null;

                                    if (order.HasDocumentLink())
                                    {
                                        verification.PdfLink = order.TryGetDocumentLink();

                                        var success = await SendToFortnox(verification);
                                        if (!success || !string.IsNullOrEmpty(ViewBag.Error))
                                        {
                                            errors.Add(order.id.ToString(), ViewBag.Error);
                                        }
                                        else if (success)
                                        {
                                            warnings.Add(
                                                order.id.ToString(),
                                                "Order finns nu i Fortnox"
                                            );
                                        }
                                    }
                                    else
                                    {
                                        errors.Add(
                                            order.id.ToString(),
                                            "Order is missing PDF Invoice link or Order Key"
                                        );
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ViewBag.Error =
                                        $"{ViewBag.Error} - {ex.InnerException?.Message}";
                                }
                            }
                            else
                            {
                                errors.Add(
                                    order.id.ToString(),
                                    $"{order.id}\n{verification.Error}"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(order.id.ToString(), ex.Message);
                        }
                    }
                }
                return View(
                    "Orders",
                    new OrderViewModel(orders, orderRoute, errors: errors, warnings: warnings)
                );
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
                string customerNr;
                try
                {
                    var customer = verification.Customer;
                    TempData["Customer"] = customer;
                    customerNr = await UpdateCustomerAsync(customer);

                    TempData["CustomerNr"] = customerNr ?? TempData["CustomerNr"];
                }
                catch (Exception ex)
                {
                    ViewBag.Error = ex.Message;
                    return false;
                }
                if (string.IsNullOrEmpty(customerNr))
                {
                    ViewBag.Error ??=
                        $"Unexpected Error for customer: {verification.Customer.Email}, order id: {verification.OrderId}.";
                    return false;
                }
                TempData["OrderItems"] = verification.OrderItems;
                try
                {
                    await Call(UpdateArticles);
                }
                catch (Exception ex)
                {
                    ViewBag.Error =
                        $"Failed to add Order Items to Fortnox for order id: {verification.OrderId}.\n{ex.Message}\n{ex.InnerException?.Message}";
                    return false;
                }

                verification.Invoice.CustomerNumber = customerNr;

                TempData["Invoice"] = verification.Invoice;
                TempData["PdfLink"] = verification.PdfLink;
                try
                {
                    await Call(CreateFortnoxInvoice);
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

        private async Task<IActionResult> CallRedirect(
            Action<FortnoxContext> action,
            string redirectUrl = "Findus"
        )
        {
            try
            {
                await _fortnox.FortnoxApiCall(action);
            }
            catch (FortnoxApiException ex)
            {
                if (
                    ex.ErrorInfo?.Message == "Invalid refresh token"
                    || ex.Message == "Fortnox Api not Connected"
                )
                {
                    return RedirectToAction("Login", "Connect", new { redirectUrl });
                }
                else
                {
                    ViewBag.Error = ex.Message;
                }
            }
            return Redirect($"{HttpContext.GenerateFullDomain()}/{redirectUrl}");
            // return View("Findus");
        }

        private async void TryGetInvoice(FortnoxContext context)
        {
            var orderId = TempData.Peek("OrderId") as string;
            var hasOrder = false;
            var invoiceCon = context.Client.InvoiceConnector;
            var invoiceSubset = await invoiceCon.FindAsync(
                new InvoiceSearch { YourOrderNumber = orderId }
            );

            // TODO: TEMPORARY:
            /*
            if (invoiceSubset?.Entities.Count > 1)
            {
                throw new Exception("Flera kunder med samma Email existerar i Fortnox.");
            } else*/if (invoiceSubset?.Entities.Count == 1)
            {
                TempData["InvoiceNumber"] = invoiceSubset?.Entities[0].DocumentNumber.ToString();
                hasOrder = true;
            }
            TempData["HasOrder"] = hasOrder;
        }

        private async Task<Invoice> GetInvoiceFromReference(long? invoiceNumber)
        {
            TempData["InvoiceNumber"] = invoiceNumber;
            await Call(GetInvoiceFromReferenceFortnox);
            Thread.Sleep(1600);
            return TempData["Invoice"] as Invoice;
        }

        private async void GetInvoiceFromReferenceFortnox(FortnoxContext context)
        {
            var invoiceNumber = TempData["InvoiceNumber"] as long?;
            var invoiceCon = context.Client.InvoiceConnector;
            // TempData["Invoice"] = await invoiceCon.GetAsync(Convert.ToInt64(invoiceNumber));
            TempData["Invoice"] = await invoiceCon.GetAsync(invoiceNumber);
        }

        private async Task<InvoiceSubset> GetInvoiceFromOrderId(string orderId)
        {
            TempData["OrderId"] = orderId;
            await Call(GetInvoiceFromOrderIdFortnox);
            Thread.Sleep(1600);
            return TempData["InvoiceSubset"] as InvoiceSubset;
        }

        private async void GetInvoiceFromOrderIdFortnox(FortnoxContext context)
        {
            var orderId = TempData.Peek("OrderId") as string;
            var invoiceCon = context.Client.InvoiceConnector;
            var invoiceSubset = await invoiceCon.FindAsync(
                new InvoiceSearch { YourOrderNumber = orderId }
            );
            TempData["InvoiceSubset"] = invoiceSubset?.Entities.FirstOrDefault();
        }

        private async Task<Invoice> AddCreditInvoiceToInvoice(long? invoiceNumber)
        {
            TempData["InvoiceNumber"] = invoiceNumber;
            await Call(AddCreditInvoiceToInvoiceFortnox);
            Thread.Sleep(1600);
            return TempData["CreditInvoice"] as Invoice;
        }

        private async void AddCreditInvoiceToInvoiceFortnox(FortnoxContext context)
        {
            var invoiceNumber = TempData["InvoiceNumber"] as long?;
            var invoiceCon = context.Client.InvoiceConnector;
            TempData["CreditInvoice"] = await invoiceCon.CreditInvoiceAsync(invoiceNumber);
        }

        private async Task<Invoice> UpdateInvoice(Invoice invoice)
        {
            TempData["Invoice"] = invoice;
            await Call(UpdateInvoiceFortnox);
            Thread.Sleep(1600);
            return TempData["Invoice"] as Invoice;
        }

        private async void UpdateInvoiceFortnox(FortnoxContext context)
        {
            var invoice = TempData["Invoice"] as Invoice;
            var invoiceCon = context.Client.InvoiceConnector;
            TempData["Invoice"] = await invoiceCon.UpdateAsync(invoice);
        }

        private async void TryGetCustomer(FortnoxContext context)
        {
            var customerEmail = TempData.Peek("CustomerEmail") as string;
            try
            {
                var customerCon = context.Client.CustomerConnector;
                var customerSubsetList = await customerCon.FindAsync(
                    new CustomerSearch { Email = customerEmail }
                );
                if (customerSubsetList?.Entities.Count > 1)
                {
                    throw new Exception("Flera kunder med samma Email existerar i Fortnox.");
                }
                if (customerSubsetList?.Entities.Count > 0)
                {
                    TempData["CustomerNr"] = customerSubsetList.Entities
                        .FirstOrDefault()
                        .CustomerNumber;
                    return;
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
            TempData.Remove("CustomerNr");
            await Call(TryGetCustomer);
            return TempData.Peek("CustomerNr") as string;
        }

        [HttpGet]
        public async Task<bool> FortnoxHasCustomer(string email)
        {
            if (string.IsNullOrEmpty(email))
                return false;
            TempData["CustomerEmail"] = email.ToLower();
            await Call(TryGetCustomer);
            // TODO: Is this needed to get CustomerNr correctly?
            Thread.Sleep(600);
            return TempData["CustomerNr"] != null
                && !string.IsNullOrEmpty(TempData["CustomerNr"] as string);
        }

        [HttpGet]
        public async Task<ActionResult> Test()
        {
            TempData["OrderId"] = "19334";
            await Call(TryGetInvoice);
            // Is this needed?
            Thread.Sleep(600);
            if (TempData["HasOrder"] == null || !(bool)TempData["HasOrder"])
            {
                throw new Exception();
            }
            return Ok();
        }

        [HttpGet]
        public async Task<bool> FortnoxHasOrder(string orderId = null)
        {
            TempData["OrderId"] = orderId;
            await Call(TryGetInvoice);
            // TODO: Neccessary for TempData to be assigned, apparently?
            Thread.Sleep(600);
            return (bool)TempData["HasOrder"];
        }

        private async Task<string> UpdateCustomerAsync(Customer customer)
        {
            TempData["Customer"] = customer;
            await Call(UpdateCustomer);
            return TempData["CustomerNr"] as string;
        }

        private async void UpdateCustomer(FortnoxContext context)
        {
            if (TempData["Customer"] is not Customer customer)
                throw new Exception("Customer is not defined.");
            var customerNr = await TryGetCustomerNr(customer.Email);

            var client = context.Client;
            var customerConn = client.CustomerConnector;

            try
            {
                if (!string.IsNullOrEmpty(customerNr))
                {
                    customer.CustomerNumber = customerNr;
                    await customerConn.UpdateAsync(customer);
                }
                else
                {
                    var newCustomer = customerConn.CreateAsync(customer).Result;
                    customerNr = newCustomer.CustomerNumber;
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"{ex.Message} ${ex.InnerException?.Message}";
            }

            TempData["CustomerNr"] = customerNr;
        }

        private async void UpdateArticles(FortnoxContext context)
        {
            // TODO: better exception error message matching in this function.

            if (TempData["OrderItems"] is not List<OrderLineItem> items)
                throw new Exception("OrderItems is not defined.");
            var articleCon = context.Client.ArticleConnector;
            foreach (var item in items)
            {
                {
                    try
                    {
                        await articleCon.CreateAsync(
                            new Article
                            {
                                ArticleNumber = item.sku,
                                Type = ArticleType.Stock,
                                Description = item.name.SanitizeStringForFortnox()
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        if (
                            !String.Equals(
                                ex.Message,
                                $"Request failed: Artikelnummer \"{item.sku}\" används redan."
                            )
                        )
                        {
                            ViewBag.Error = ex.InnerException?.Message ?? ex.Message;
                        }
                    }
                }
            }
        }

        private async Task<byte[]> FetchFile(string fileLink)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var pdf = await httpClient.FetchFile(fileLink);
            httpClient.Dispose();
            return pdf;
        }

        private async void CreateFortnoxInvoice(FortnoxContext context)
        {
            if (TempData["Invoice"] is not Invoice invoice)
                throw new Exception("Invoice is not defined.");

            var invoiceCon = context.Client.InvoiceConnector;
            try
            {
                invoice = await invoiceCon.CreateAsync(invoice);
                /*
                await _wcOrderApi.AddInvoiceReferenceAsync(
                    invoice.YourOrderNumber,
                    invoice.DocumentNumber
                );
                */

                // Send PDF Invoice
                if (TempData["PdfLink"] is string pdfLink)
                {
                    var pdf = await FetchFile(pdfLink);
                    await UploadPDF(
                        pdf,
                        invoice.DocumentNumber,
                        Convert.ToUInt64(invoice.YourOrderNumber)
                    );
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.InnerException?.Message ?? ex.Message;
                // throw;
            }
        }
    }
}
