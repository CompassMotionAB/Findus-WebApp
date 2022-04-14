using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Findus.Models;
using Fortnox.SDK.Entities;
using WooCommerceNET.WooCommerce.v2;
using Customer = Fortnox.SDK.Entities.Customer;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace Findus.Helpers
{
    public static class VerificationUtils
    {
        private static readonly List<string> EUCountries =
            new()
            {
                "ES",
                "BG",
                "HU",
                "LV",
                "PL",
                "CZ",
                "MT",
                "IT",
                "SI",
                "IE",
                "SE",
                "DK",
                "FI",
                "CY",
                "LU",
                "RO",
                "EE",
                "GR",
                "LT",
                "FR",
                "HR",
                "BE",
                "NL",
                "SK",
                "DE",
                "PT",
                "AT"
            };

        public static bool IsInsideEU(string isoCode)
        {
            return EUCountries.Contains(isoCode.ToUpper());
        }

        internal static bool ContainsNoReducedRate(List<OrderLineItem> line_items)
        {
            if (
                !line_items.TrueForAll(
                    o =>
                        o.tax_class == "reduced-rate"
                        || o.tax_class == "normal-rate"
                        || string.IsNullOrEmpty(o.tax_class)
                )
            )
            {
                throw new Exception(
                    "Tax Class in Items of Order are only expected to have either 'normal-rate', 'reduced-rate' or empty ( normal rate )"
                );
            }
            return line_items.TrueForAll(o => o.tax_class != "reduced-rate");
        }

        public static string GetPaymentMethod(WcOrder order)
        {
            var payment = order.payment_method.ToLower();

            if (
                string.IsNullOrEmpty(payment) /*  && !IsInsideEU(order.billing.country) */
            )
            {
                throw new Exception("Beställningen behöver bokföras manuellt.");
            }

            // NOTE: Catch-all: stripe & stripe_{bancontant,ideal,sofort}
            if (new Regex(@"^stripe\S*").IsMatch(payment))
                return "Stripe";
            // NOTE: Catch-all: paypal & (ppec_paypal)_paypal
            if (new Regex(@"^\S*paypal$").IsMatch(payment))
                return "PayPal";

            return payment switch
            {
                "paypal" => "PayPal",
                _
                  => throw new Exception(
                      String.Format(
                          "Payment Method: '{0}' unexpected."
                              + Environment.NewLine
                              + "Payment Method Title: {1}",
                          order.payment_method,
                          order.payment_method_title
                      )
                  ),
            };
        }

        public static bool OnlyReducedRate(List<OrderLineItem> line_items)
        {
            if (
                !line_items.TrueForAll(
                    o =>
                        o.tax_class == "reduced-rate"
                        || o.tax_class == "normal-rate"
                        || string.IsNullOrEmpty(o.tax_class)
                )
            )
            {
                throw new Exception(
                    "Tax Class in Items of Order are only expected to have either 'normal-rate', 'reduced-rate' or empty ( normal rate )"
                );
            }
            return line_items.TrueForAll(o => o.tax_class == "reduced-rate");
        }

        public static Invoice GenInvoice(
            WcOrder order,
            decimal currencyRate,
            AccountsModel accounts,
            string customerNr = null
        )
        {
            if (order.date_paid == null)
                throw new Exception($"Order Id: {order.id} is missing final payment date");
            if (order.line_items == null || order.line_items.Count < 1)
                throw new Exception($"Order Id: {order.id} is missing items in order");
            if (!string.Equals(order.currency, "EUR", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Expected WooCommerce order to be in EUR");

            var invoiceRows = new List<InvoiceRow>();

            /*
            order.line_items.ForEach(i =>
               invoiceRows.Add(new InvoiceRow
               {
                   Price = i.GetTotalWithTax() * currencyRate,
                   ArticleNumber = i.sku,
                   DeliveredQuantity = i.quantity,
               })
            );
            */

            var salesAccount = accounts.GetSalesAccount(order);

            return new Invoice()
            {
                CustomerNumber = customerNr,
                InvoiceType = InvoiceType.CashInvoice,
                InvoiceDate = order.date_paid,
                PaymentWay = PaymentWay.Card,
                VATIncluded = true,
                Currency = "SEK",
                CurrencyRate = 1,
                YourOrderNumber = order.id.ToString(),
                //YourReference = order.customer_id?.ToString(), //TODO: Should this be used?

                CustomerName = $"{order.billing.first_name} {order.billing.last_name}".Trim(),
                Country = String.Equals(
                    order.billing.country,
                    "SE",
                    StringComparison.OrdinalIgnoreCase
                )
                  ? "Sweden "
                  : CountryUtils.GetEnglishName(order.billing.country),
                Address1 = order.billing.address_1,
                Address2 = order.billing.address_2,
                ZipCode = order.billing.postcode,
                City = order.billing.city,
                DeliveryCountry = String.Equals(
                    order.billing.country,
                    "SE",
                    StringComparison.OrdinalIgnoreCase
                )
                  ? "Sweden "
                  : CountryUtils.GetEnglishName(order.billing.country),
                DeliveryAddress1 = order.shipping.address_1,
                DeliveryAddress2 = order.shipping.address_2,
                DeliveryZipCode = order.shipping.postcode,
                DeliveryCity = order.shipping.city,
                InvoiceRows = invoiceRows,
            }.GenInvoiceRows(
                new InvoiceData
                {
                    Accounts = accounts,
                    SalesAcc = salesAccount,
                    Order = order,
                    CountryIso = order.billing.country
                }
            );
        }

        public static decimal GetAccurateCartTax(this WcOrder order)
        {
            decimal total = (decimal)order.line_items.Sum(i => i.GetAccurateTaxTotal());
            //total += (decimal)(order.shipping_total + order.shipping_tax);

            var diff = MathF.Abs((float)(total - order.cart_tax));
            // Should not deviate more than 0.01 from WooCommerce total cost
            if (diff >= 0.01)
            {
                throw new Exception(
                    $"WooCommerce order cart tax does not match calculated cart tax. Difference: {total:0.00}, {order.cart_tax:0.00} = {diff:0.000}"
                );
            }
            return total;
        }

        public static decimal GetTotalWithTax(this OrderLineItem item) =>
            (decimal)item.price + item.GetAccurateTaxTotal();

        public static decimal GetAccurateTaxTotal(this OrderLineItem item) =>
            (decimal)item.taxes.Sum(t => t.total);

        public static decimal GetAccurateTotal(this WcOrder order)
        {
            decimal total = (decimal)order.line_items.Sum(
                i => (i.price * i.quantity) + i.GetAccurateTaxTotal()
            );
            total += (decimal)(order.shipping_total + order.shipping_tax);

            var diff = MathF.Abs((float)(total - order.total));
            // Should not deviate more than 0.001 from WooCommerce total cost
            if (diff > 0.001)
            {
                throw new Exception(
                    $"WooCommerce order total does not match calculated total. Difference: {total:0.5F}, {order.total:0.5F} = {diff:0.5F}"
                );
            }
            return total;
        }

        public static decimal GetTotalItemsTax(this WcOrder order) =>
            order.line_items.Sum(i => i.GetAccurateTaxTotal());

        public static InvoiceAccrual GenInvoiceAccrual(
            WcOrder order,
            AccountsModel accounts,
            decimal currencyRate,
            decimal? accurateTotal = null,
            bool simplify = false,
            string customerNr = null,
            long? invoiceNr = null,
            string period = null
        )
        {
            var vatAccount = accounts.GetVATAccount(order);
            var salesAccount = accounts.GetSalesAccount(order);

            var countryIso = order.billing.country;

            bool isInEu = IsInsideEU(countryIso);
            bool isStandard = ContainsNoReducedRate(order.line_items);
            bool isReduced = !isStandard;
            bool hasShippingCost = order.shipping_lines.Count > 0 && order.shipping_total > 0;

            if (order.fee_lines != null && order.fee_lines.Count != 0)
                throw new Exception("WooCommerce order contains unexpected 'fee_lines'");

            decimal total = accurateTotal ?? order.GetAccurateTotal();

            decimal totalCartTax = order.GetAccurateCartTax();
            decimal cartTaxSEK = totalCartTax * currencyRate;

            decimal totalItemsTax = order.GetTotalItemsTax();
            decimal totalItemsTaxSEK = totalItemsTax * currencyRate;

            decimal shippingSEK = (decimal)order.shipping_total * currencyRate;
            decimal shippingVatSEK = (decimal)order.shipping_tax * currencyRate;
            decimal totalSEK = total * currencyRate;

            var paymentMethod = GetPaymentMethod(order);

            var invAccrualData = new InvoiceData
            {
                CustomerNumber = customerNr,
                InvoiceNumber = invoiceNr,
                Period = "MONTHLY",
                Order = order,
                CountryIso = countryIso,
                IsInEu = isInEu,
                IsReduced = isReduced,
                IsStandard = isStandard,
                HasShippingCost = hasShippingCost,
                Accounts = accounts,
                SalesAcc = salesAccount,
                VatAcc = vatAccount,
                CurrencyRate = currencyRate,
                Total = total,
                CartTaxSEK = cartTaxSEK,
                TotalItemsTaxSEK = totalItemsTaxSEK,
                ShippingSEK = shippingSEK,
                ShippingVatSEK = shippingVatSEK,
                TotalSEK = totalSEK,
                PaymentMethod = paymentMethod,
            };

            var inv = GenInvoiceAccrual(invAccrualData);
            if (simplify)
            {
                inv.TrySymplify();
            }
            return inv
            //.TryAddCartTax(invAccrualData)
            .TryVerifyRows()
                .DEBUGAddMissing()
                .AddPaymentFee(invAccrualData);
            /*
            var inv = GenInvoiceAccrual(invAccrualData);
            try {
                return inv.TryVerifyRows();
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                return inv;
            }
            */
        }

        public static decimal GetTotal(this IEnumerable<InvoiceAccrualRow> rows)
        {
            return rows.Sum(
                    r =>
                    {
                        if (r.Credit > 0.0M && r.Debit > 0.0M)
                        {
                            throw new Exception(
                                "Invoice Accrual Row cannot contain both Credit and Debit"
                            );
                        }
                        return r.Credit + r.Debit;
                    }
                ) ?? 0.0M;
        }

        public static InvoiceAccrual TryVerifyRows(this InvoiceAccrual inv)
        {
            var creditRows = inv.InvoiceAccrualRows.Where(r => r.Debit == null || r.Debit == 0.0M);
            var debitRows = inv.InvoiceAccrualRows.Where(r => r.Credit == null || r.Credit == 0.0M);

            var creditTotal = creditRows.GetTotal();

            creditRows = creditRows
                .GroupBy(r => r.Account)
                .Select(
                    g =>
                        new InvoiceAccrualRow
                        {
                            Account = g.Key,
                            Debit = 0,
                            Credit = g.Sum(r => r.Credit),
                            TransactionInformation = g.FirstOrDefault().TransactionInformation
                        }
                );

            var sumOfRows = creditRows.GetTotal();
            decimal diff = (decimal)MathF.Abs((float)(sumOfRows - creditTotal));
            var EPSILON = new decimal(1, 0, 0, false, 25); //1e-25m;
            if (diff > EPSILON)
            {
                throw new Exception(
                    $"Total Credit(s) does not match expected Credit(s) in SEK, Expected: {creditTotal:0.00}, got {sumOfRows:0.00}, Difference: {MathF.Abs((float)(creditTotal - sumOfRows))}"
                );
            }
            var debitTotal = debitRows.GetTotal();
            diff = (decimal)MathF.Abs((float)(creditTotal - debitTotal));
            if (diff >= 0.01M)
            {
                throw new Exception(
                    $"Total Debit does not match Credit(s) in SEK, Credit: {creditTotal:0.00}, Debit: {debitTotal:0.00}, Difference: {diff}"
                );
            }

            return inv;
        }

        public static decimal? GetTotalCredit(this IEnumerable<InvoiceAccrualRow> rows)
        {
            return rows.Where(r => r.Credit != 0).Sum(r => r.Credit);
        }

        public static decimal? GetTotalDebit(this IEnumerable<InvoiceAccrualRow> rows)
        {
            return rows.Where(r => r.Debit != 0).Sum(r => r.Debit);
        }

        private static IEnumerable<InvoiceAccrualRow> TrySimplify(
            this IEnumerable<InvoiceAccrualRow> rows
        )
        {
            if (rows.GetTotalDebit() != 0 && rows.GetTotalCredit() != 0)
            {
                throw new Exception(
                    "Cannot Concatenate Invoice Accrual Rows than contain both Debit and Credit."
                );
            }
            return rows.GroupBy(r => r.Account)
                .Select(
                    g =>
                        new InvoiceAccrualRow
                        {
                            Account = g.Key,
                            Debit = g.Sum(r => r.Debit),
                            Credit = g.Sum(r => r.Credit),
                            TransactionInformation = g.FirstOrDefault().TransactionInformation
                        }
                );
        }

        public static InvoiceAccrual TrySymplify(this InvoiceAccrual inv, bool sort = false)
        {
            var creditRows = inv.InvoiceAccrualRows.Where(r => r.Credit != 0.0M);
            var debitRows = inv.InvoiceAccrualRows.Where(r => r.Debit != 0.0M);

            creditRows = creditRows.TrySimplify();
            debitRows = debitRows.TrySimplify();

            // Sort Revenue/Fee Payment Accounts to the Top
            if (sort)
            {
                creditRows = creditRows
                    .OrderBy(r => r.Account != 1780)
                    .OrderBy(r => r.Account != 1580);
                debitRows = debitRows
                    .OrderBy(r => r.Account != 6570)
                    .OrderBy(r => r.Account != 1580);
            }

            inv.InvoiceAccrualRows = debitRows.Concat(creditRows).ToList();

            return inv;
        }

        public static InvoiceAccrual ConcatInvoices(
            this Dictionary<string, InvoiceAccrual> invoices
        )
        {
            //var rows = new IEnumerable<InvoiceAccrualRow>();
            IEnumerable<InvoiceAccrualRow> rows = new List<InvoiceAccrualRow>();
            foreach (var (id, invoice) in invoices)
            {
                if (invoice?.InvoiceAccrualRows != null)
                {
                    rows = rows.Concat(invoice.InvoiceAccrualRows);
                }
            }

            return new InvoiceAccrual { InvoiceAccrualRows = rows.ToList() };
        }

        public static InvoiceAccrual TryAddCartTax(this InvoiceAccrual invoice, InvoiceData data)
        {
            if (data.CartTaxSEK > 0.0M)
            {
                var vatAcc = data.GetVatAcc(countryIso: data.CountryIso);
                invoice.AddRow(vatAcc.AccountNr, credit: data.CartTaxSEK, info: "Cart Tax");
            }
            return invoice;
        }

        public static InvoiceAccrual AddPaymentFee(this InvoiceAccrual invoice, InvoiceData data)
        {
            return invoice.AddPaymentFee(
                data.Order,
                data.GetSalesAcc(isStandard: true, paymentMethod: data.PaymentMethod),
                data.PaymentMethod
            );
        }

        public static InvoiceAccrual AddPaymentFee(
            this InvoiceAccrual invoice,
            WcOrder order,
            RateModel salesAccount,
            string paymentMethod = null
        )
        {
            paymentMethod = paymentMethod ??= GetPaymentMethod(order);
            decimal fee = paymentMethod switch
            {
                "Stripe"
                  => decimal.Parse(
                      (string)order.meta_data.First(d => d.key == "_stripe_fee").value
                  ),
                "PayPal"
                  => decimal.Parse(
                      (string)order.meta_data.First(d => d.key == "_paypal_transaction_fee").value
                  ),
                _ => 0.0M
            };
            if (fee <= 0.0M || fee >= (decimal)order.total)
            {
                throw new Exception($"Unexpected Fee amount: {fee} ,({paymentMethod})");
            }
            invoice.AddRow(
                salesAccount.AccountNr,
                credit: fee,
                info: $"{paymentMethod} Avgift - Utgående"
            );
            invoice.AddRow(6570, debit: fee, info: $"{paymentMethod} Avgift");
            return invoice;
        }

        public static Customer GetCustomer(this Invoice invoice, WcOrder order)
        {
            return new Customer
            {
                Name = invoice.CustomerName,
                Type = CustomerType.Private,
                Email = order.billing.email,
                CountryCode = order.shipping.country.ToUpper(),

                Address1 = invoice.Address1,
                Address2 = invoice.Address2,
                City = invoice.City,

                //Ref = order.customer_id != 0 ? order.customer_id.ToString() : null,
                DeliveryName = invoice.DeliveryName,
                DeliveryAddress1 = invoice.DeliveryAddress1,
                DeliveryAddress2 = invoice.DeliveryAddress2,
                DeliveryCity = invoice.DeliveryCity,
                DeliveryZipCode = invoice.DeliveryZipCode,
            };
        }

        public static InvoiceAccrual DEBUGAddMissing(this InvoiceAccrual invoice)
        {
            invoice.AccrualAccount = invoice.InvoiceAccrualRows[0].Account;
            invoice.Total = invoice.InvoiceAccrualRows.GetTotalDebit();
            invoice.RevenueAccount = 0001;
            return invoice;
        }

        private static Invoice GenInvoiceRows(this Invoice invoice, InvoiceData data)
        {
            foreach (var item in data.Items())
            {
                // TODO: Verify that only flat discount (promo code) could attribute to 0 cost item
                if (item.price == 0.0M && item.subtotal_tax == 0.0M)
                    continue;
                var isStandard = item.tax_class != "reduced-rate";
                var salesAcc = data.GetSalesAcc(isStandard, countryIso: data.CountryIso);
                var taxLabel = data.GetTaxLabel(salesAcc, isStandard: isStandard);
                invoice.AddRow(
                    salesAcc.AccountNr,
                    item.sku,
                    item.quantity ?? 1, // TODO: Is this safe to assume?
                    price: item.GetTotalWithTax(),
                    vat: salesAcc.Rate * 100M,
                    info: $"Försäljning - {taxLabel}"
                );
            }
            return invoice;
        }

        private static InvoiceAccrual GenInvoiceAccrual(InvoiceData data)
        {
            var inv = new InvoiceAccrual()
            {
                Description = $"Faktura för order id: {data.Order.id}",
                Period = data.Period,
                InvoiceNumber = data.InvoiceNumber,
                StartDate = data.Order.date_completed,
                EndDate = data.Order.date_completed,
                InvoiceAccrualRows = new List<InvoiceAccrualRow>()
            };
            if (data.IsInEu)
            {
                var salesAccNr = data.GetSalesAcc(paymentMethod: data.PaymentMethod).AccountNr;
                inv.AddRow(salesAccNr, debit: data.TotalSEK, info: data.PaymentMethod);
                if (data.HasShippingCost)
                {
                    var vatAcc = data.GetVatAcc(countryIso: data.CountryIso);
                    var taxLabel = data.GetTaxLabel(vatAcc);
                    inv.AddRow(
                        vatAcc.AccountNr,
                        credit: data.ShippingTotalSEK,
                        info: $"Fraktkostnad med Moms - {taxLabel}"
                    );
                }

                foreach (var item in data.Items())
                {
                    if (item.price == 0.0M && item.subtotal_tax == 0.0M)
                        continue;
                    var isStandard = item.tax_class != "reduced-rate";
                    var salesAcc = data.GetSalesAcc(isStandard, countryIso: data.CountryIso);
                    var taxLabel = data.GetTaxLabel(salesAcc, isStandard: isStandard);
                    inv.AddRow(
                        salesAcc.AccountNr,
                        credit: (decimal)(item.price * item.quantity) * data.CurrencyRate,
                        info: $"Försäljning - {taxLabel}"
                    );
                    if (item.subtotal_tax > 0.0M)
                    {
                        var vatAcc = data.GetVatAcc(isStandard, countryIso: data.CountryIso);
                        taxLabel = data.GetTaxLabel(vatAcc, isStandard: isStandard);

                        var accurateItemTax = item.GetAccurateTaxTotal() / item.quantity ?? 1;

                        var diff = MathF.Abs((float)(accurateItemTax - (item.price * vatAcc.Rate)));
                        // Should not deviate more than 0.01 from WooCommerce subtotal_tax
                        if (diff >= 0.01)
                        {
                            throw new Exception(
                                $"WooCommerce order tax does not match calculated {taxLabel}. Difference: {item.subtotal_tax}, {item.price * vatAcc.Rate} = {diff:0.000}"
                            );
                        }
                        //var itemCost = ((decimal)item.price + accurateTax) * vatAcc.Rate;
                        /* Added Vat sales to first debit row, see ItemsTaxSEK */
                        /*
                        inv.AddRow(
                            salesAccNr,
                            debit: accurateTax * data.CurrencyRate,
                            info: $"Moms - {taxLabel}"
                        );*/
                        inv.AddRow(
                            vatAcc.AccountNr,
                            credit: accurateItemTax * (item.quantity ?? 1) * data.CurrencyRate,
                            info: $"Utgående Moms - {taxLabel}"
                        );
                    }
                }
            }
            else
            {
                if (!data.TotalStandardRateEquals(0.0M))
                {
                    throw new Exception("Expected Rate to be 0% for countries outside EU.");
                }
                var salesAcc = data.GetSalesAcc(paymentMethod: data.PaymentMethod);
                inv.AddRow(
                    salesAcc.AccountNr,
                    debit: data.TotalSEK + data.ShippingSEK,
                    info: data.PaymentMethod
                );

                var vatAcc = data.GetVatAcc(countryIso: data.CountryIso);
                inv.AddRow(
                    vatAcc.AccountNr,
                    credit: data.TotalSEK,
                    info: $"Utanför EU - Moms - {0.0:P2}"
                );

                if (data.HasShippingCost)
                {
                    vatAcc = data.GetVatAcc(countryIso: data.CountryIso);
                    inv.AddRow(vatAcc.AccountNr, credit: data.ShippingSEK, info: "Fraktkostnad");
                }
            }

            return inv;
        }

        public static VerificationModel Verify(
            WcOrder order,
            AccountsModel accounts,
            decimal currencyRate,
            bool simplify = false
        )
        {
            var result = new VerificationModel()
            {
                OrderId = order.id.ToString(),
                OrderItems = order.line_items
            };
            try
            {
                var accurateTotal = order.GetAccurateTotal();
                result.InvoiceAccrual = GenInvoiceAccrual(
                    order,
                    accounts,
                    currencyRate,
                    accurateTotal,
                    simplify
                );
                result.Invoice = GenInvoice(order, currencyRate, accounts);
                result.Customer = GetCustomer(result.Invoice, order);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        public static decimal GetTaxRate(this OrderTaxLine taxLine)
        {
            var taxLabel = taxLine.label;
            try
            {
                return decimal.Parse(taxLabel[..taxLabel.IndexOf("%")]) / 100.0M;
            }
            catch (Exception)
            {
                throw new Exception($"Received unsupported Tax label from WooCommerce: {taxLabel}");
            }
        }

        public static string VerifyRate(
            RateModel acc,
            WcOrder order,
            OrderLineItem item = null,
            bool? isStandard = null
        )
        {
            if (acc?.Rate == 0 || acc?.AccountNr == 0)
            {
                return "0% Moms";
            }
            if (item != null)
            {
                isStandard = item.tax_class != "reduced-rate";
            }
            else if (isStandard == null)
            {
                throw new ArgumentNullException(
                    paramName: nameof(isStandard),
                    message: $"Expected {nameof(isStandard)} to be provided when {nameof(item)} == null"
                );
            }

            var taxRates = order.tax_lines.Select(t => t.GetTaxRate()).OrderByDescending(t => t);
            decimal wcTax = isStandard switch
            {
                true => taxRates.FirstOrDefault(),
                false => taxRates.LastOrDefault(),
            };

            if (acc.Rate != wcTax)
            {
                throw new Exception(
                    $"VAT Rate miss-match, expected value: {acc.Rate:P2} VAT, but WooCommerce gave: {wcTax:P2} VAT"
                );
            }
            return $"{wcTax:P2} Moms";
        }
    }
}
