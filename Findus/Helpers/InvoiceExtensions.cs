using System.Collections.Generic;
using Findus.Models;
using Fortnox.SDK.Entities;
using WooCommerceNET.WooCommerce.v2;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace Findus.Helpers
{
    public struct Discount
    {
        public decimal Amount;
        public DiscountType Type;
    }

    public static class InvoiceExtensions
    {
        public static void AddRow(
            this Invoice invoice,
            int accountNr,
            string sku,
            decimal quantity,
            decimal price,
            decimal vat,
            Discount discount = default,
            string info = null
        )
        {
            var newRow = new InvoiceRow
            {
                AccountNumber = accountNr,
                ArticleNumber = sku,
                DeliveredQuantity = quantity,
                Price = price,
                Description = info,
                VAT = vat,
            };
            if (!discount.Equals(default(Discount)))
            {
                newRow.Discount = discount.Amount;
                newRow.DiscountType = discount.Type;
            }

            invoice.InvoiceRows.Add(newRow);
        }
        public static Invoice SetInvoiceRows(
            this Invoice invoice,
            WcOrder order,
            AccountsModel accounts
        )
        {
            var rows = new List<InvoiceRow>();
            foreach (var item in order.line_items)
            {
                var account = accounts.GetSalesAccount(order, item);
                rows.Add(
                    new InvoiceRow
                    {
                        AccountNumber = account.AccountNr,
                        DeliveredQuantity = item.quantity,
                        Price = item.price,
                        Discount = 0,
                        ArticleNumber = item.sku,
                        Description = FortnoxStringUtil.SanitizeStringForFortnox(item.name)
                    }
                );
            }

            invoice.InvoiceRows = rows;
            return invoice;
        }
    }
}
