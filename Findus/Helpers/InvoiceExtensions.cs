using System.Collections.Generic;
using Fortnox.SDK.Entities;

namespace Findus.Helpers
{
    public struct Discount
    {
        public decimal Amount;
        public DiscountType Type;
    }
    public static class InvoiceExtensions
    {
        public static void AddRow(this Invoice invoice, int accountNr, string sku, decimal quantity, decimal price, decimal vat, Discount discount = default, string info = null)
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
    }
}