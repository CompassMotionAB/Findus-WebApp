using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fortnox.SDK.Entities;
using Fortnox.SDK.Interfaces;
using Fortnox.SDK.Search;
using FindusWebApp.Helpers;

namespace FindusWebApp.Extensions
{
    public static class FortnoxConnectorExtensions
    {
        public static async Task<List<InvoiceSubset>> GetInvoices(this IInvoiceConnector connector, string customerNr = null, int minPerPage = 5, int maxPerPage = 20)
        {
            var searchSettings = new InvoiceSearch
            {
                CustomerNumber = customerNr,
                Limit = maxPerPage,
                SortBy = Sort.By.Invoice.InvoiceDate,
                SortOrder = Sort.Order.Ascending
            };

            var largeInvoiceCollection = await connector.FindAsync(searchSettings);
            var totalInvoices = largeInvoiceCollection.TotalResources;

            var neededPages = HttpUtilities.GetNeededPages(minPerPage, maxPerPage, totalInvoices);
            var mergedCollection = new List<InvoiceSubset>();

            for (var i = 0; i < neededPages; i++)
            {
                searchSettings.Limit = minPerPage;
                searchSettings.Page = i + 1;
                var smallInvoiceCollection = await connector.FindAsync(searchSettings);
                mergedCollection.AddRange(smallInvoiceCollection.Entities);
            }
            return mergedCollection;
        }
        public static async Task<List<CustomerSubset>> GetCustomers(this ICustomerConnector connector, string customerNr = null, int minPerPage = 5, int maxPerPage = 20)
        {
            var searchSettings = new CustomerSearch
            {
                CustomerNumber = customerNr,
                Limit = maxPerPage,
                SortBy = Sort.By.Customer.CustomerNumber,
                SortOrder = Sort.Order.Ascending
            };

            var largeCustomerCollection = await connector.FindAsync(searchSettings);
            var totalCustomers = largeCustomerCollection.TotalResources;

            var neededPages = HttpUtilities.GetNeededPages(minPerPage, maxPerPage, totalCustomers);
            var mergedCollection = new List<CustomerSubset>();

            for (var i = 0; i < neededPages; i++)
            {
                searchSettings.Limit = minPerPage;
                searchSettings.Page = i + 1;
                var smallCustomerCollection = await connector.FindAsync(searchSettings);
                mergedCollection.AddRange(smallCustomerCollection.Entities);
            }
            return mergedCollection;
        }

        /*
        private static async Task<List<TEntitySubset>> GetEntitiesForPagesAsync<TEntity,TEntitySubset, TConnector, TSearchSettings>(this EntityCollection<TEntitySubset> collection, TConnector connector, TSearchSettings searchSettings,int minPerPage, int maxPerPage)
        where TEntitySubset : class
        where TConnector : SearchableEntityConnector<TEntity, TEntitySubset, TSearchSettings>
        where TSearchSettings : BaseSearch, new()
        {
            var totalSize = collection.TotalResources;
            var neededPages = Utilities.GetNeededPages(minPerPage, maxPerPage, totalSize);

            var mergedCollection = new List<CustomerSubset>();
            for (var i = 0; i < neededPages; i++)
            {
                searchSettings.Limit = minPerPage;
                searchSettings.Page = i + 1;
                var smallCustomerCollection = await connector.FindAsync(searchSettings);
                mergedCollection.AddRange(smallCustomerCollection.Entities);
            }
            return mergedCollection;
        }
        */
    }
}