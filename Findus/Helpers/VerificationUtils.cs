using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Findus.Models;
using Fortnox.SDK.Entities;
using WooCommerceNET.WooCommerce.v2;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace Findus.Helpers
{
    public static class VerificationUtils
    {
        private static readonly List<string> EUCountries = new List<string>()
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
            if (!line_items.TrueForAll(o => o.tax_class == "reduced-rate" || o.tax_class == "normal-rate" || string.IsNullOrEmpty(o.tax_class)))
            {
                throw new Exception("Tax Class in Items of Order are only expected to have either 'normal-rate', 'reduced-rate' or empty ( normal rate )");
            }
            return line_items.TrueForAll(o => o.tax_class != "reduced-rate");
        }

        public static string GetPaymentMethod(WcOrder order)
        {
            var payment = order.payment_method.ToLower();

            // NOTE: Catch-all: stripe & stripe_{bancontant,ideal}
            return new Regex(@"^stripe\S*").IsMatch(payment)
                ? "Stripe"
                : payment switch
                {
                    "paypal" => "PayPal",
                    _ => throw new Exception(String.Format(
                         "Payment Method: '{0}' unexpected." + Environment.NewLine +
                         "Payment Method Title: {1}",
                         order.payment_method,
                         order.payment_method_title)
                     ),
                };
        }

        public static bool OnlyReducedRate(List<OrderLineItem> line_items)
        {
            if (!line_items.TrueForAll(o => o.tax_class == "reduced-rate" || o.tax_class == "normal-rate" || string.IsNullOrEmpty(o.tax_class)))
            {
                throw new Exception("Tax Class in Items of Order are only expected to have either 'normal-rate', 'reduced-rate' or empty ( normal rate )");
            }
            return line_items.TrueForAll(o => o.tax_class == "reduced-rate");
        }

        public static Invoice GenInvoice(WcOrder order, decimal currencyRate, decimal? accurateTotal = null)
        {
            if (order.date_paid == null) throw new Exception($"Order Id: {order.id} is missing final payment date");
            if (order.line_items == null || order.line_items.Count < 1) throw new Exception($"Order Id: {order.id} is missing items in order");

            var invoiceRows = new List<InvoiceRow>();
            order.line_items.ForEach(i =>
                invoiceRows.Add(new InvoiceRow
                {
                    Price = i.price,
                })
            );

            return new Invoice()
            {
                Currency = "SEK",//order.currency.ToUpper(),
                CurrencyRate = currencyRate,
                FinalPayDate = (DateTime)order.date_paid,
                YourOrderNumber = order.id.ToString(),
                YourReference = order.billing.email,

                CustomerName = (order.billing.first_name + $" {order.billing.last_name}").Trim(),
                Address1 = order.billing.address_1,
                Address2 = order.billing.address_2,
                ZipCode = order.billing.postcode,
                City = order.billing.city,
            };
        }

        private static string VerifyCoupon(OrderCouponLine coupon)
        {
            if ((coupon.discount != null && coupon.discount != 0.0M) || (coupon.discount_tax != null && coupon.discount_tax != 0.0M))
            {
                throw new Exception($"Unexpected discount amount: {coupon.discount} for discount code: {coupon.code}");
            }

            if (coupon.code == null)
            {
                throw new Exception("Order is missing discount code for applied discount");
            }

            return coupon.code;
        }

        public static bool HasFreeShaker(this WcOrder order) => order.coupon_lines.Any(coupon => coupon.code == "freeshaker");

        public static decimal GetAccurateTotal(this WcOrder order)
        {
            decimal total = (decimal)order.line_items.Sum(i => (i.price * i.quantity) + i.taxes.Sum(t => t.total));
            float diff = MathF.Abs((float)(total - (decimal)(order.total - order.shipping_total)));

            // Should not deviate more than 0.01 from WooCommerce total cost
            if (diff > 0.01)
            {
                throw new Exception(
                    string.Format("WooCommerce order total does not match calculated total. Difference: {0}, {1} = {2:0.000}",
                        total,
                        order.total,
                        diff
                ));
            }
            return total;
        }

        public static InvoiceAccrual GenInvoiceAccrual(WcOrder order, AccountsModel accounts, decimal currencyRate, decimal? accurateTotal = null)
        {
            var VATAccount = accounts.GetVATAccount(order);
            var SalesAccount = accounts.GetSalesAccount(order);

            var countryIso = order.billing.country;

            bool isInEu = VerificationUtils.IsInsideEU(countryIso);
            bool isStandard = VerificationUtils.ContainsNoReducedRate(order.line_items);
            bool isReduced = !isStandard;
            bool hasShippingCost = order.shipping_lines.Count > 0 && order.shipping_total > 0;

            if (order.fee_lines != null && order.fee_lines.Count != 0)
                throw new Exception("WooCommerce order contains unexpected 'fee_lines'");
            if (order.discount_total != null && order.discount_total != 0.0M)
                throw new Exception("WooCommerce order contains unexpected 'discount_total'");

            foreach (var coupon in order.coupon_lines)
            {
                VerifyCoupon(coupon);
                _ = coupon.code switch
                {
                    "freeshaker" => true,
                    _ => throw new Exception($"WooCommerce order contains unexpected discount code: {coupon.code}"),
                };
            }

            decimal total = accurateTotal ?? order.GetAccurateTotal();

            decimal shippingSEK = (decimal)order.shipping_total * currencyRate;
            decimal shippingVatSEK = (decimal)order.shipping_tax * currencyRate;
            decimal totalSEK = total * currencyRate;

            var paymentMethod = GetPaymentMethod(order);

            var inv = new InvoiceAccrual()
            {
                InvoiceAccrualRows = new List<InvoiceAccrualRow>()
            };

            if (isInEu)
            {
                var salesAccNr = accounts.GetSales(paymentMethod, isStandard).AccountNr;
                inv.AddRow(
                    salesAccNr,
                    debit: totalSEK + shippingSEK,
                    info: paymentMethod
                );

                if (hasShippingCost)
                {
                    var vatAcc = accounts.GetVAT(paymentMethod, isStandard);
                    var vatAccNr = vatAcc.AccountNr;
                    var taxLabel = VerifyRate(vatAcc, order, isReduced: isReduced);
                    inv.AddRow(
                        vatAccNr,
                        credit: (decimal)(order.shipping_total - order.shipping_tax) * currencyRate,
                        info: "Fraktkostnad"
                    );
                    inv.AddRow(
                        vatAccNr,
                        credit: (decimal)order.shipping_tax * currencyRate,
                        info: $"Fraktkostnad VAT {taxLabel}"
                    );
                }
                foreach (var item in order.line_items.OrderByDescending(i => i.price))
                {
                    if (item.tax_class == "reduced-rate")
                    {
                        var acc = SalesAccount.Reduced;
                        var taxLabel = VerifyRate(acc, order, item: item);

                        inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.price * item.quantity) * currencyRate, // * (decimal)acc.Rate
                                info: $"Försäljning - {taxLabel}"
                            );
                        if (item.subtotal_tax > 0.0M)
                        {
                            acc = VATAccount.Reduced;
                            inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.subtotal_tax) * currencyRate, // * (decimal)acc.Rate
                                info: $"Utgående Moms - {taxLabel}"
                        );
                        }
                    }
                    else
                    {
                        var acc = SalesAccount.Standard;
                        var taxLabel = VerifyRate(acc, order, item: item);
                        inv.AddRow(
                            acc.AccountNr,
                            credit: (decimal)(item.price * item.quantity) * currencyRate, // * (decimal)acc.Rate
                            info: $"Försäljning - {taxLabel}"
                    );
                        if (item.subtotal_tax > 0.0M)
                        {
                            acc = VATAccount.Standard;
                            inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.subtotal_tax) * currencyRate, // * (decimal)acc.Rate
                                info: $"Utgående Moms - {taxLabel}"
                        );
                        }
                    }
                }
            }
            else
            {
                var vatAcc = VATAccount.Standard;
                var salesAcc = SalesAccount.Standard;
                if (vatAcc.Rate + salesAcc.Rate > 0.0M)
                {
                    throw new Exception("Expected Rate to be 0.0% for countries outside EU.");
                }
                inv.AddRow(
                                salesAcc.AccountNr,
                                debit: totalSEK + shippingSEK,
                                info: paymentMethod
                        );
                inv.AddRow(
                                vatAcc.AccountNr,
                                credit: totalSEK,
                                info: $"Utanför EU - Moms - {0.0:P2}"
                        );
                if (hasShippingCost)
                {
                    if (isReduced) vatAcc = accounts.GetVATAccount(paymentMethod).Reduced;
                    inv.AddRow(
                            vatAcc.AccountNr,
                            credit: shippingSEK,
                            info: "Fraktkostnad"
                    );
                }
            }

            return inv.AddPaymentFee(order, SalesAccount, paymentMethod);
        }

        public static InvoiceAccrual AddPaymentFee(this InvoiceAccrual invoice, WcOrder order, AccountModel salesAccount, string paymentMethod = null)
        {
            if (string.IsNullOrEmpty(paymentMethod))
            {
                paymentMethod = GetPaymentMethod(order);
            }
            decimal fee = paymentMethod switch
            {
                "Stripe" => decimal.Parse((string)order.meta_data.First(d => d.key == "_stripe_fee").value),
                "PayPal" => throw new NotImplementedException(),
                _ => 0.0M
            };
            if (fee > 0.0M)
            {
                invoice.AddRow(salesAccount.Standard.AccountNr, credit: fee, info: $"{paymentMethod} Avgift");
                invoice.AddRow(6570, debit: fee, info: $"{paymentMethod} Avgift");
            }
            return invoice;
        }

        private static string VerifyRate(RateModel acc, WcOrder order, OrderLineItem item = null, bool? isReduced = null)
        {
            if (item != null)
            {
                isReduced = item.tax_class == "reduced-rate";
            }
            else if (isReduced == null)
            {
                throw new ArgumentNullException();
            }

            var taxLine = order.tax_lines[
                isReduced == true
                ? 0
                : 1];

            string taxLabel = taxLine.label;

            decimal wcTax = decimal.Parse(taxLabel[..taxLabel.IndexOf("%")]) / 100.0M;
            if (acc.Rate != wcTax)
            {
                throw new Exception($"VAT Rate miss-match, expected value: {acc.Rate:P2} VAT, but WooCommerce gave: {taxLabel}");
            }
            return taxLabel;
        }
    }
}
