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
    public class InvoiceData
    {
        public WcOrder Order;

        public IOrderedEnumerable<OrderLineItem> Items()
        {
            return Order.line_items.OrderByDescending(i => i.price);
        }

        public string GetTaxLabel(RateModel account, bool? isStandard = null)
        {
            return VerificationUtils.VerifyRate(account, Order, isStandard: isStandard ?? IsStandard);
        }

        public bool TotalStandardRateEquals(decimal value = 0.0M)
        {
            if (IsInEu)
            {
                return GetSalesAcc(IsStandard).Rate + GetVatAcc(IsStandard).Rate == value;
            }
            else
            {
                return GetSalesAcc(IsStandard, PaymentMethod).Rate + GetVatAcc(IsStandard, PaymentMethod).Rate == value;
            }
        }

        private string countryIso;
        public AccountsModel Accounts { get; internal set; }
        public AccountModel SalesAcc;
        public AccountModel VatAcc;

        /// <summary>
        /// <returns>
        /// Returns either Reduced or Standard Sales Account
        /// Always returns the highest TAX Rate of the order, assuming that reduced VAT <= standard VAT.
        /// If NO items in order has reduced rate, the order is considered Standard rate.
        /// </returns>
        /// </summary>
        public RateModel GetSalesAcc(bool? isStandard = null, string paymentMethod = null, string countryIso = null)
        {
            isStandard ??= IsStandard;
            if (!string.IsNullOrEmpty(paymentMethod)) return Accounts.GetSales(paymentMethod, isStandard: (bool)isStandard);
            return Accounts.GetSales(countryIso, isStandard: (bool)isStandard);
        }

        /// <summary>
        /// <returns>
        /// Returns either Reduced or Standard VAT Account
        /// Always returns the highest TAX Rate of the order, assuming that reduced VAT <= standard VAT.
        /// If NO items in order has reduced rate, the order is considered Standard rate.
        /// </returns>
        /// </summary>
        public RateModel GetVatAcc(bool? isStandard = null, string paymentMethod = null, string countryIso = null)
        {
            isStandard ??= IsStandard;
            if (!string.IsNullOrEmpty(paymentMethod)) return Accounts.GetVAT(paymentMethod, isStandard: (bool)isStandard);
            return Accounts.GetVAT(countryIso, isStandard: (bool)isStandard);
        }

        public bool HasShippingCost;
        public bool IsStandard;
        public bool IsReduced;
        public bool HasDiscounts;

        public decimal CurrencyRate;
        public decimal Total;
        public decimal ShippingSEK;
        public decimal ShippingVatSEK;
        public decimal ShippingTotalSEK => ShippingSEK + ShippingVatSEK;

        public decimal ShippingCost;

        public decimal CartTaxSEK { get; internal set; }
        public decimal TotalItemsTaxSEK { get; internal set; }
        public string CountryIso { get => countryIso ?? Order.billing.country; set => countryIso = value; }

        public decimal TotalSEK;
        public string PaymentMethod;
        public bool IsInEu;
        public string CustomerNumber;
        public long? InvoiceNumber;
        public string Period;
    }
}
