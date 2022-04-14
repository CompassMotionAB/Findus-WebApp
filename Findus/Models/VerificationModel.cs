using System.Collections.Generic;
using WooCommerceNET;
using WooCommerceNET.WooCommerce.v2;
using Fortnox.SDK.Entities;
using Customer = Fortnox.SDK.Entities.Customer;
using System;

namespace Findus.Models {
    public class VerificationModel {
        public VerificationModel() {
            TimeStamp = DateTime.Now;
        }
        private readonly DateTime TimeStamp;

        public bool IsValid()
        {
            if ((DateTime.Now - TimeStamp).TotalMinutes >= 20 || DateTime.Now < TimeStamp)
            {
                Error = "Verification has expired.";
                return false;
            } else if(Customer == null) {
                Error = "Verification is missing Customer.";
                return false;
            }
            return String.IsNullOrEmpty(Error);
        }

        public Invoice Invoice;
        public InvoiceAccrual InvoiceAccrual;
        public Customer Customer;
        public decimal AccurateTotal;
        public decimal CurrencyRate;

        public List<OrderLineItem> OrderItems;
        public string Error;

        public string OrderId;
    }
}