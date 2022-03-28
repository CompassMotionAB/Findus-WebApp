using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FindusWebApp.Services.Fortnox;
using Fortnox.SDK.Entities;
using System.Collections.Generic;
using FindusWebApp.Extensions;
using Fortnox.SDK.Exceptions;
using Fortnox.SDK.Serialization;
using FindusWebApp.Models;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using System.Net.Http;
using Fortnox.SDK.Search;
using Fortnox.SDK.Interfaces;

namespace FindusWebApp.Controllers
{
    public class FortnoxController : Controller
    {
        private readonly IFortnoxServices _services;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;

        public FortnoxController(IFortnoxServices services, IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
        {
            _services = services;
            _memoryCache = memoryCache;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> IndexAsync(string customerNr = null)
        {
            await Call(FetchCompanyName);
            // NOTE: Will redirect to "/Connect/Login" if Fornox is not authenticated:
            TempData["CustomerNr"] = customerNr;
            return await CallRedirect(GetCustomersPage);
        }
        private async Task Call(Action<FortnoxContext> action)
        {
            try
            {
                await _services.FortnoxApiCall(action);
            }
            catch (FortnoxApiException ex)
            {
                ViewBag.Error = ex.Message;
            }
        }
        private async Task<IActionResult> CallRedirect(Action<FortnoxContext> action)
        {
            try
            {
                await _services.FortnoxApiCall(action);
            }
            catch (FortnoxApiException ex)
            {
                if (ex.ErrorInfo?.Message == "Invalid refresh token")
                {
                    return RedirectToAction("Login", "Connect", new { redirectUrl = "Fortnox" });
                }
                else if (ex.Message == "Fortnox Api not Connected")
                {
                    return RedirectToAction("Login", "Connect", new { redirectUrl = "Fortnox" });
                }
                else
                {
                    ViewBag.Error = ex.Message;
                }
            }
            return View("Fortnox");
        }

        /*
        private async Task FetchCustomersAndInvoicesAsync(string customerNr)
        {
            var cacheKey = customerNr ?? "0";
            var invoices = await FetchInvoicesAsync(customerNr);
            var customers = await FetchCustomersAsync(customerNr);
            _customerInvoices = invoices.GroupBy(
                i => i.CustomerNumber).ToDictionary(t => t.Key, grp => grp.ToArray());
        }
        */
        private void FetchCustomers(FortnoxContext context)
        {
            var customerNr = TempData["CustomerNr"] as string;
            TempData["CustomerSubset"] = context.Client.CustomerConnector.GetCustomers(customerNr).Result;
        }
        private void FetchInvoices(FortnoxContext context)
        {
            var customerNr = TempData["CustomerNr"] as string;
            TempData["InvoiceSubset"] = context.Client.InvoiceConnector.GetInvoices(customerNr).Result;
        }
        private void FetchAccrualInvoices(FortnoxContext context)
        {
            var fromDate = TempData["FromDate"] as string;
            var toDate = TempData["ToDate"] as string;
            TempData["InvoiceAccrualSubset"] = context
                .Client
                .InvoiceAccrualConnector
                .GetAccrualInvoices(
                    DateTime.Parse(fromDate),
                    DateTime.Parse(toDate))
                .Result;
        }

        private async Task FetchAsync<TEntitySubset>(string customerNr, string fromDate = null, string toDate = null)
        {
            string entityName = typeof(TEntitySubset)?.Name;

            TempData["CustomerNr"] = customerNr;
            var cacheKey = TempData.Peek("CacheKey") as string;
            if (string.IsNullOrEmpty(cacheKey))
            {
                cacheKey = entityName + "_" + (customerNr ?? $"{fromDate}_{toDate}");
            }
            if (!_memoryCache.TryGetValue(cacheKey, out object entities) || entities == null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(5));
                if (entityName == "InvoiceSubset")
                {
                    await Call(FetchInvoices);
                }
                else if (entityName == "CustomerSubset")
                {
                    await Call(FetchCustomers);
                }
                else if (entityName == "InvoiceAccrualSubset")
                {
                    if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate)) throw new Exception("Missing to/from date for fetching Invoice Accruals");
                    TempData["FromDate"] = fromDate;
                    TempData["ToDate"] = toDate;
                    await Call(FetchAccrualInvoices);
                }
                else
                {
                    throw new ArgumentException("Unexpected type: " + entityName ?? nameof(TEntitySubset));
                }
                _memoryCache.Set(cacheKey, TempData.Peek(entityName), cacheEntryOptions);
                return;
            }
            TempData[entityName] = entities;// as TEntitySubset[];
        }
        private async void GetCustomersPage(FortnoxContext context)
        {
            TempData["CacheKey"] = "CustomerPage";
            var customerNr = TempData.Peek("CustomerNr") as string;
            await FetchAsync<CustomerSubset>(customerNr);
            if (!string.IsNullOrEmpty(customerNr))
                await FetchAsync<InvoiceSubset>(customerNr);
            TempData.Remove("CacheKey");
        }
        public async Task<IActionResult> Customer(string customerNr = null)
        {
            TempData["CustomerNr"] = customerNr;
            return await CallRedirect(GetCustomersPage);
        }
        public void FetchCompanyName(FortnoxContext context)
        {
            var client = context.Client;
            var conn = client.CompanyInformationConnector;
            ViewData["CompanyName"] = conn.GetAsync().Result.CompanyName;
        }

        public async Task<ActionResult> InvoiceAccruals(string dateFrom = null, string dateTo = null)
        {
            try
            {
                await FetchAsync<InvoiceAccrualSubset>(null, dateFrom, dateTo);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return View("InvoiceAccruals");
        }

        [HttpGet]
        public async Task<ActionResult> CustomerInvoices(string customerNr)
        {
            try
            {
                //TempData["CustomerNr"] = customerNr;
                await FetchAsync<InvoiceSubset>(customerNr);
                //await Call(FetchInvoices);
                return View("Partial/InvoiceSubsetList", TempData["InvoiceSubset"]);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return new EmptyResult();
        }

        #region HelperMethods
        public async Task<IActionResult> CreateInvoice(string customerNr)
        {
            TempData["CustomerNr"] = customerNr;
            await Call(CreateNewInvoice);
            return View("Fortnox");
        }

        [HttpGet]
        public async Task<ActionResult> DeleteCustomer(string customerNr)
        {
            TempData["CustomerNr"] = customerNr;
            try
            {
                await Call(DeleteCustomerAsync);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View("Fortnox");
            }
            return RedirectToAction("Index");
        }

        private async void DeleteCustomerAsync(FortnoxContext context)
        {
            var customerNr = TempData["CustomerNr"] as string;
            if (String.IsNullOrEmpty(customerNr)) throw new Exception("Missing CustomerNr in TempData");
            await context.Client.CustomerConnector.DeleteAsync(customerNr);
        }
        public void GetCustomer(FortnoxContext context)
        {
            var customerEmail = TempData.Peek("CustomerEmail") as string;
            var customerCon = context.Client.CustomerConnector;
            TempData["CustomerSubset"] = customerCon.FindAsync(new CustomerSearch() { Email = customerEmail, });
        }

        private void CreateNewInvoice(FortnoxContext context)
        {
            var customerNr = TempData["CustomerNr"] as string;
            if (String.IsNullOrEmpty(customerNr)) throw new Exception("Invalid Customer Number.");

            var client = context.Client;

            var tmpArticle = client.ArticleConnector.CreateAsync(new Article() { Description = "TmpArticle", Type = ArticleType.Stock, PurchasePrice = 100 }).Result;
            var newInvoice = new Invoice()
            {
                CustomerNumber = customerNr,
                InvoiceDate = new DateTime(2019, 1, 20),
                DueDate = new DateTime(2022, 2, 20),
                InvoiceType = InvoiceType.CashInvoice,
                PaymentWay = PaymentWay.Cash,
                Comments = "TestInvoice",
                InvoiceRows = new List<InvoiceRow>()
                {
                    new InvoiceRow(){ ArticleNumber = tmpArticle.ArticleNumber, DeliveredQuantity = 10, Price = 100},
                    new InvoiceRow(){ ArticleNumber = tmpArticle.ArticleNumber, DeliveredQuantity = 20, Price = 100},
                    new InvoiceRow(){ ArticleNumber = tmpArticle.ArticleNumber, DeliveredQuantity = 15, Price = 100}
                }
            };
            newInvoice = client.InvoiceConnector.CreateAsync(newInvoice).Result;
            ViewData["InvoiceInfo"] = "Invoice with ID: " + newInvoice.DocumentNumber + " created successfully.";
        }
        #endregion
    }
}