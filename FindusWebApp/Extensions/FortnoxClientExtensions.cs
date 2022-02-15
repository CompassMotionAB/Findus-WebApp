using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fortnox.SDK.Connectors.Base;
using Fortnox.SDK.Entities;
using Fortnox.SDK.Search;
using Fortnox.SDK;
using Fortnox.SDK.Interfaces;

namespace FindusWebApp.Extensions
{
    public static class FortnoxClientExtensions
    {
        public static async Task<EntityCollection<CustomerSubset>> GetAllCustomersAsync(this FortnoxClient fortnoxClient)
        {
            var connector = fortnoxClient.CustomerConnector;
            var searchSettings = new CustomerSearch() { Limit = ApiConstants.Unlimited };
            return await connector.FindAsync(searchSettings);
        }
        public static async Task<List<CustomerSubset>> GetCustomerPages(this FortnoxClient fortnoxClient, string customerNr = null,int minPerPage = 5, int maxPerPage = 20)
        {
            var connector = fortnoxClient.CustomerConnector;
            return await connector.GetCustomers(customerNr, minPerPage, maxPerPage);
        }
        public static async Task<List<InvoiceSubset>> GetInvoicePages(this FortnoxClient fortnoxClient, string customerNr = null, int minPerPage = 5, int maxPerPage = 20)
        {
            var connector = fortnoxClient.InvoiceConnector;
            return await connector.GetInvoices(customerNr, minPerPage, maxPerPage);
        }
    }
}