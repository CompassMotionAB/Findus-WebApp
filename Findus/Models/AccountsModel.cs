using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Findus.Helpers;
using Newtonsoft.Json;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace Findus.Models
{
    public class RateModel
    {
        [JsonProperty("rate", Required = Required.DisallowNull)]
        public decimal Rate { get; set; }

        [JsonProperty("account", Required = Required.DisallowNull)]
        public int AccountNr { get; set; }

        //public string TaxType { get; set; } // Standard/Reduced
    }
    public class AccountModel
    {
        [JsonProperty("standard", Required = Required.DisallowNull)]
        public RateModel Standard { get; set; }
        [JsonProperty("reduced", Required = Required.DisallowNull)]
        public RateModel Reduced { get; set; }
    }

    public class AccountsModel
    {
        public AccountsModel(Dictionary<string, AccountModel> vat, Dictionary<string, AccountModel> sales)
        {
            VAT = vat;
            Sales = sales;
            //VAT = Utilities.LoadJson<Dictionary<string, AccountModel>>("VATAccounts.json");
            //Sales = Utilities.LoadJson<Dictionary<string, AccountModel>>("SalesAccounts.json");
        }
        [DisplayName("Försäljnings Konton")]
        public Dictionary<string, AccountModel> VAT { get; set; }

        [DisplayName("Moms Konton")]
        public Dictionary<string, AccountModel> Sales { get; set; }

        public AccountModel GetVATAccount(WcOrder order)
        {
            var countryIso = order.billing.country;

            return GetAccount(VAT, countryIso);
        }
        public AccountModel GetSalesAccount(WcOrder order)
        {
            var countryIso = order.billing.country;
            var paymentMethod = VerificationUtils.GetPaymentMethod(order);

            return GetAccount(Sales, countryIso, paymentMethod);
        }
        private static AccountModel GetAccount(Dictionary<string, AccountModel> accounts, string countryIso, string payment_method = null!)
        {
            if (accounts.ContainsKey(countryIso))
            {
                return accounts[countryIso];
            }
            if (/* string.IsNullOrEmpty(payment_method) &&  */accounts.ContainsKey("NON_EU"))
            {
                return accounts["NON_EU"];
            }
            if (string.IsNullOrEmpty(payment_method))
            {
                throw new ArgumentException("Payment Method required for countries outside EU: Expected Stripe or PayPal");
            }
            return accounts[payment_method];
        }

        public AccountModel GetSalesAccount(string countryIso)
        {
            return Sales[countryIso];
        }
        public AccountModel GetVATAccount(string countryIso)
        {
            return VAT[countryIso];
        }

        public RateModel GetSales(string countryIso, bool isStandard = false, bool isReduced = false) {
            if(isStandard && isReduced) throw new Exception("Account rate can only be either standard or reduced at once.");
            if(!Sales.ContainsKey(countryIso)) countryIso = "NON_EU";
            return isStandard ? Sales[countryIso].Standard : Sales[countryIso].Reduced;
        }
        public RateModel GetVAT(string countryIso, bool isStandard = false, bool isReduced = false) {
            if(isStandard && isReduced) throw new Exception("Account rate can only be either standard or reduced at once.");
            if(!VAT.ContainsKey(countryIso)) countryIso = "NON_EU";
            return isStandard ? VAT[countryIso].Standard : VAT[countryIso].Reduced;
        }
    }
}