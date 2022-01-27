using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FortnoxApiExample.Helper;
using FortnoxApiExample.Models;
using FortnoxApiExample.Services.Fortnox;
using Fortnox.SDK.Exceptions;
using Fortnox.SDK.Entities;
using Fortnox.SDK.Authorization;
using Fortnox.SDK;


// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FortnoxApiExample.Controllers
{
    public class FortnoxController : Controller
    {
        private readonly IFortnoxServices _services;

        public FortnoxController(IFortnoxServices services)
        {
            _services = services;
        }

        // GET: /<controller>/
        public IActionResult Index()
        {
            // TODO: Redirect to Connect/Login if user is not logged in.
            // Alternatively, use AuthFilter to only allow logged in users to access to page.
            return View("Fortnox");
        }

        public async Task<IActionResult> CreateCustomerAsync()
        {
            await _services.FortnoxApiCall(new Action<FortnoxContext>(CreateCustomer));
            return View("Fortnox");
        }

        public async Task<IActionResult> CreateCustomerAndInvoiceAsync()
        {
            await call(new Action<FortnoxContext>(CreateCustomerAndInvoice));
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
                //ViewData["ErrorMessage"] = ex.Message;
                throw ex;
            }
        }

        #region HelperMethods

        private void CreateCustomer(FortnoxContext context)
        {
            var client = new FortnoxClient(new StandardAuth(context.GetAccessToken()));
            var customerConn = client.CustomerConnector;
            /*
            var customerConn = context.CustomerConnector;
            //Customer customer = Inputs.CreateCustomer(customerConnector).Result;
            */
            CustomerSubset customer = customerConn.FindAsync(null).Result.Entities.FirstOrDefault();

            ViewData["CustomerInfo"] = "Customer with ID: " + customer.CustomerNumber + " created successfully.";
            ViewData["CustomerId"] = customer.CustomerNumber;
        }

        private void CreateCustomerAndInvoice(FortnoxContext context)
        {
            var client = context.Client;
            Invoice newInvoice = Inputs.CreateCustomerAndInvoice(
                client.CustomerConnector,
                client.ArticleConnector,
                client.InvoiceConnector
                ).Result;
            ViewData["InvoiceInfo"] = "Invoice with ID: " + newInvoice.DocumentNumber + " created successfully.";
        }
        /*

        private void CreateNewInvoice(ServiceContext context)
        {
            var dataService = new DataService(context);
            var queryService = new QueryService<Account>(context);
            var customerService = new QueryService<Customer>(context);
            var query = "Select * from Customer where Id='" + id + "'";
            var queryResponse = customerService.ExecuteIdsQuery(query).ToList();
            if (queryResponse != null)
            {
                var invoice = Inputs.CreateInvoice(dataService, queryService, queryResponse[0]);
                ViewData["InvoiceInfo"] = "Invoice with ID:" + invoice.Id + " created successfully";
            }
            else
            {
                ViewData["InvoiceInfo"] = "Invalid Customer information";
            }

            ViewData["CustomerId"] = id;
        }
        */

        #endregion
    }
}