using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fortnox.SDK.Entities;
using Fortnox.SDK.Interfaces;

namespace FortnoxApiExample.Helper
{
    public class Inputs
    {
        internal static async Task<Customer> CreateCustomer(ICustomerConnector customerConnector)
        {
            var rnd = new Random();
            var newCust = new Customer
            {
                Name = "Testing" + rnd.NextDouble(),
                OurReference = "Testing" + rnd.NextDouble(),
                YourReference = "Testing" + rnd.NextDouble()
            };
            return await customerConnector.CreateAsync(newCust);
        }
        internal static async Task<Invoice> CreateCustomerAndInvoice(ICustomerConnector customerConnector, IArticleConnector articleConnector, IInvoiceConnector invoiceConnector) {
            var tmpCustomer = await customerConnector.CreateAsync(new Customer() { Name = "TmpCustomer", CountryCode = "SE", City = "Testopolis" });
            var tmpArticle = await articleConnector.CreateAsync(new Article() { Description = "TmpArticle", Type = ArticleType.Stock, PurchasePrice = 100 });

            var newInvoice = new Invoice()
            {
                CustomerNumber = tmpCustomer.CustomerNumber,
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
            return await invoiceConnector.CreateAsync(newInvoice);
        }
        /*

        internal static Invoice CreateInvoice(DataService dataService, QueryService<Account> queryService,
            Customer customer)
        {
            var item = ItemCreate(dataService, queryService);
            var line = new Line
            {
                DetailType = LineDetailTypeEnum.SalesItemLineDetail,
                DetailTypeSpecified = true,
                Description = "Sample for Reimburse Charge with Invoice.",
                Amount = new decimal(40),
                AmountSpecified = true
            };
            var lineDetail = new SalesItemLineDetail
            {
                ItemRef = new ReferenceType {name = item.Name, Value = item.Id}
            };
            line.AnyIntuitObject = lineDetail;

            Line[] lines = {line};

            var invoice = new Invoice
            {
                Line = lines,
                CustomerRef = new ReferenceType {name = customer.DisplayName, Value = customer.Id},
                TxnDate = DateTime.Now.Date
            };

            var response = dataService.Add(invoice);
            return response;
        }


        #region Helper methods

        internal static Item ItemCreate(DataService dataService, QueryService<Account> queryService)
        {
            var random = new Random();
            var expenseAccount = QueryOrAddAccount(dataService, queryService,
                "select * from account where AccountType='Cost of Goods Sold'", AccountTypeEnum.CostofGoodsSold,
                AccountClassificationEnum.Expense, AccountSubTypeEnum.SuppliesMaterialsCogs);
            var incomeAccount = QueryOrAddAccount(dataService, queryService,
                "select * from account where AccountType='Income'", AccountTypeEnum.Income,
                AccountClassificationEnum.Revenue, AccountSubTypeEnum.SalesOfProductIncome);
            var item = new Item
            {
                Name = "Item_" + random.NextDouble(),
                ExpenseAccountRef = new ReferenceType {name = expenseAccount.Name, Value = expenseAccount.Id},
                IncomeAccountRef = new ReferenceType {name = incomeAccount.Name, Value = incomeAccount.Id},
                Type = ItemTypeEnum.NonInventory,
                TypeSpecified = true,
                UnitPrice = new decimal(100.0),
                UnitPriceSpecified = true
            };

            var apiResponse = dataService.Add(item);
            return apiResponse;
        }

        internal static Account QueryOrAddAccount(DataService dataService, QueryService<Account> queryService,
            string query, AccountTypeEnum accountType, AccountClassificationEnum classification,
            AccountSubTypeEnum subType)
        {
            var queryResponse = queryService.ExecuteIdsQuery(query).ToList();

            if (queryResponse.Count == 0)
            {
                var account = AccountCreate(dataService, accountType, classification, subType);
                return account;
            }

            return queryResponse[0];
        }

        internal static Account AccountCreate(DataService dataService, AccountTypeEnum accountType,
            AccountClassificationEnum classification, AccountSubTypeEnum subType)
        {
            var random = new Random();
            var account = new Account
            {
                Name = "Account_" + random.NextDouble(),
                AccountType = accountType,
                AccountTypeSpecified = true,
                Classification = classification,
                ClassificationSpecified = true,
                AccountSubType = subType.ToString(),
                SubAccountSpecified = true
            };
            var apiResponse = dataService.Add(account);
            return apiResponse;
        }

        #endregion
        */
    }
}