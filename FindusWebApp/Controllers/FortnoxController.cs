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
                await FetchAsync<InvoiceSubset>(customerNr);
                return View("Partial/InvoiceSubsetList", TempData["InvoiceSubset"]);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }
            return new EmptyResult();
        }
    }
}