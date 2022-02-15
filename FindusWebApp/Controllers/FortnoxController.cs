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

        public async Task<IActionResult> IndexAsync()
        {
            await Call(FetchCompanyName);
            // NOTE: Will redirect to "/Connect/Login" if Fornox is not authenticated:
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
                ViewData["ErrorMessage"] = ex.Message;
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
                    ViewData["ErrorMessage"] = ex.Message;
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

        private async Task FetchAsync<TEntitySubset>(string customerNr)
        {
            string entityName = typeof(TEntitySubset)?.Name;

            TempData["CustomerNr"] = customerNr;
            var cacheKey = customerNr ?? TempData.Peek("CacheKey") as string;
            cacheKey += "-" + entityName;
            if (!_memoryCache.TryGetValue(cacheKey, out object entities) || entities == null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(8));
                if (entityName == "InvoiceSubset")
                {
                    await Call(FetchInvoices);
                    _memoryCache.Set(cacheKey, TempData.Peek("InvoiceSubset"), cacheEntryOptions);
                }
                else if (entityName == "CustomerSubset")
                {
                    await Call(FetchCustomers);
                    _memoryCache.Set(cacheKey, TempData.Peek("CustomerSubset"), cacheEntryOptions);
                }
                else
                {
                    throw new ArgumentException("Unexpected type: " + entityName ?? nameof(TEntitySubset));
                }
                return;
            }
            TempData[entityName] = entities;// as TEntitySubset[];
        }
        private async void GetCustomersPage(FortnoxContext context)
        {
            TempData["CacheKey"] = "CustomerPage";
            var customerNr = TempData.Peek("CustomerNr") as string;
            await FetchAsync<CustomerSubset>(customerNr);
            if(!string.IsNullOrEmpty(customerNr))
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
                ViewData["ErrorInfo"] = ex.Message;
            }
            return new EmptyResult();
        }

        #region HelperMethods
        public async Task<IActionResult> CreateCustomer()
        {
            await Call(CreateNewCustomer);
            return View("Fortnox");
        }
        public async Task<IActionResult> CreateInvoice(string customerNr)
        {
            TempData["CustomerNr"] = customerNr;
            await Call(CreateNewInvoice);
            return View("Fortnox");
        }

        private void CreateNewCustomer(FortnoxContext context)
        {
            var client = context.Client;
            var customerConn = client.CustomerConnector;
            var rnd = new Random();
            var newCust = new Customer
            {
                Name = "Testing" + rnd.NextDouble(),
                OurReference = "Testing" + rnd.NextDouble(),
                YourReference = "Testing" + rnd.NextDouble()
            };
            var customer = customerConn.CreateAsync(newCust).Result;

            ViewData["CustomerInfo"] = "Customer with ID: " + customer.CustomerNumber + " created successfully.";
            ViewData["CustomerNr"] = customer.CustomerNumber;
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