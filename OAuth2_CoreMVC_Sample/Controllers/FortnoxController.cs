using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FortnoxApiExample.Services.Fortnox;
using Fortnox.SDK.Entities;
using System.Collections.Generic;

namespace FortnoxApiExample.Controllers
{
    public class FortnoxController : Controller
    {
        private readonly IFortnoxServices _services;
        private string id;

        public FortnoxController(IFortnoxServices services)
        {
            _services = services;
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            return View("Fortnox");
        }

        public async Task<IActionResult> CreateCustomer()
        {
            await _services.FortnoxApiCall(new Action<FortnoxContext>(CreateNewCustomer));
            return View("Fortnox");
        }
        public async Task<IActionResult> CreateInvoice(string customerNr)
        {
            id = customerNr;
            await _services.FortnoxApiCall(new Action<FortnoxContext>(CreateNewInvoice));
            return View("Fortnox");
        }

        private async Task call(Action<FortnoxContext> action)
        {
            try
            {
                await _services.FortnoxApiCall(action);
            }
            catch (Exception ex)
            {
                ViewData["ErrorMessage"] = ex.Message;
            }
        }

        #region HelperMethods

        private void CreateNewCustomer(FortnoxContext context)
        {
            var customerConn = context.CustomerConnector;
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
            var client = context.Client;
            if (String.IsNullOrEmpty(id)) throw new Exception("Expected Customer Number to be defined.");

            var tmpArticle = client.ArticleConnector.CreateAsync(new Article() { Description = "TmpArticle", Type = ArticleType.Stock, PurchasePrice = 100 }).Result;
            var newInvoice = new Invoice()
            {
                CustomerNumber = id,
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