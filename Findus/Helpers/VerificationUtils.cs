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

        internal static bool OnlyStandardRate(List<OrderLineItem> line_items)
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

            // Catch-all: stripe & stripe_{bancontant,ideal}
            if (new Regex(@"^stripe\S*").IsMatch(payment))
            {
                return "Stripe";
            }
            return payment switch
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

        public static Invoice GenInvoice(WcOrder order, decimal currencyRate)
        {
            if (order.date_paid == null) throw new Exception($"Order Id: {order.id} is missing final payment date");
            if (order.line_items == null || order.line_items.Count < 1) throw new Exception($"Order Id: {order.id} is missing items in order");

            return new Invoice()
            {
                Currency = order.currency.ToUpper(),
                CurrencyRate = currencyRate,
                FinalPayDate = (DateTime)order.date_paid,
                YourOrderNumber = order.id.ToString(),
                YourReference = order.customer_id.ToString(),

                CustomerName = order.billing.first_name + order.billing.last_name ?? $" {order.billing.last_name}",
                Address1 = order.billing.address_1,
                Address2 = order.billing.address_2,
                ZipCode = order.billing.postcode,
                City = order.billing.city,
            };
        }

        public static InvoiceAccrual GenInvoiceAccrual(WcOrder order, AccountsModel accounts, decimal currencyRate)
        {
            var VATAccount = accounts.GetVATAccount(order);
            var SalesAccount = accounts.GetSalesAccount(order);

            var countryIso = order.billing.country;

            bool isInEu = VerificationUtils.IsInsideEU(countryIso);
            bool isStandard = VerificationUtils.OnlyStandardRate(order.line_items);
            bool isReduced = !isStandard;
            bool hasFreeShaker = false;
            bool hasShippingCost = order.shipping_lines.Count > 0 && order.shipping_total > 0;

            if (order.fee_lines != null && order.fee_lines.Count != 0)
                throw new Exception("WooCommerce order contains unexpected 'fee_lines'");
            if (order.discount_total != null && order.discount_total != 0.0M)
                throw new Exception("WooCommerce order contains unexpected 'discount_total'");
            if (order.coupon_lines != null && order.coupon_lines.Count != 0)
            {
                var discount = order.coupon_lines.FirstOrDefault();
                if (discount!.code == null)
                {
                    throw new Exception($"Order Id: {order.id} is missing discount code for applied discount");
                }

                hasFreeShaker = discount.code switch
                {
                    "freeshaker" => true,
                    "blackcherry" => throw new NotImplementedException(),
                    _ => throw new Exception($"WooCommerce order contains unexpected discount code: {discount.code}"),
                };
            }

            // Most accurate total:
            decimal total = (decimal)order.line_items.Sum(i => (i.price + i.subtotal_tax) * i.quantity);
            //total -= (decimal)order.cart_tax;
            decimal totalShip = (decimal)order.shipping_total;
            decimal totalShipVat = (decimal)order.shipping_tax;

            float diff = MathF.Abs((float)(total - (decimal)(order.total - totalShip)));

            if (hasFreeShaker)
            {
                // (!) NOTE: Assumes the price of shaker is 18 EUR
                total -= (decimal)order.line_items.Sum(i => i.sku == "NAU007" ? i.quantity * 18.0M : 0.0M);
            }
            // Should not deviate more than 0.01 from WooCommerce total cost
            if (diff > 0.01)
            {
                throw new Exception(
                    string.Format("WooCommerce order total does not match calculated total. Difference: {0}, {1} = {2:0.000}{3}",
                        total,
                        order.total,
                        diff,
                        hasFreeShaker ? " NOTE: Order contains free shaker(s), SKU: NAU007." : null
                    )
                );
            }
            decimal shippingSEK = totalShip * currencyRate;
            decimal shippingVatSEK = totalShipVat * currencyRate;
            decimal totalSEK = total * currencyRate;

            var paymentMethod = GetPaymentMethod(order);

            var inv = new InvoiceAccrual();

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
                    inv.AddRow(
                        vatAccNr,
                        credit: (decimal)(order.shipping_total - order.shipping_tax) * currencyRate,
                        info: "Fraktkostnad"
                    );
                    inv.AddRow(
                        vatAccNr,
                        credit: (decimal)order.shipping_tax * currencyRate,
                        info: $"Fraktkostnad VAT {vatAcc.Rate:P2}"
                    );
                }
                foreach (var item in order.line_items)
                {
                    if (hasFreeShaker && item.sku == "NAU007") continue;
                    if (item.tax_class == "reduced-rate")
                    {
                        var acc = SalesAccount.Reduced;
                        VerifyRate(acc, order, item);

                        inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.price * item.quantity) * currencyRate, // * (decimal)acc.Rate
                                info: $"Försäljning - {acc.Rate:P2}"
                            );
                        if (item.subtotal_tax > 0.0M)
                        {
                            acc = VATAccount.Reduced;
                            inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.subtotal_tax) * currencyRate, // * (decimal)acc.Rate
                                info: $"Utgående Moms - {acc.Rate:P2}"
                        );
                        }
                    }
                    else
                    {
                        var acc = SalesAccount.Standard;
                        VerifyRate(acc, order, item);
                        inv.AddRow(
                            acc.AccountNr,
                            credit: (decimal)(item.price * item.quantity) * currencyRate, // * (decimal)acc.Rate
                            info: $"Försäljning - {acc.Rate:P2}"
                    );
                        if (item.subtotal_tax > 0.0M)
                        {
                            acc = VATAccount.Standard;
                            inv.AddRow(
                                acc.AccountNr,
                                credit: (decimal)(item.subtotal_tax) * currencyRate, // * (decimal)acc.Rate
                                info: $"Utgående Moms - {acc.Rate:P2}"
                        );
                        }
                    }
                }
            }
            else
            {
                var vatAcc = VATAccount.Standard;
                var salesAcc = SalesAccount.Standard;
                //var salesAcc = accounts.getSalesAccount("NON_EU").Standard;
                if (vatAcc.Rate + salesAcc.Rate > 0.0M)
                {
                    throw new Exception("Expected Rate to be 0.0% for countries outside EU.");
                }
                // If country is outside EU and Rate is 0%, use AccountNr: 3105 ( defined in SalesAccount["NON_EU"])
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

            return inv;
        }

        private static void VerifyRate(RateModel acc, WcOrder order, OrderLineItem item)
        {
            string taxLabel = order.tax_lines[
                item.tax_class == "reduced-rate"
                ? 0
                : 1].label;

            decimal wcTax = decimal.Parse(taxLabel[..taxLabel.IndexOf("%")]) / 100.0M;
            if (acc.Rate != wcTax)
            {
                throw new Exception($"VAT Rate miss-match, expected value: {acc.Rate:P2} VAT, but WooCommerce gave: {taxLabel}");
            }
        }
    }
}
