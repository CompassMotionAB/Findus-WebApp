using System;
using System.Collections.Generic;
using System.Linq;
using Findus.Helpers;
using Findus.Models;
using Fortnox.SDK.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace FindusWebApp.Models
{
    public class OrderViewModel
    {
        public Dictionary<string, InvoiceAccrual> InvoiceAccruals;
        public InvoiceAccrual Invoice {get; set; }
        public decimal? TotalDebit {get; set; }
        public decimal? TotalCredit {get; set; }

        public IEnumerable<SelectListItem> OrderSelect
        {
            get
            {
                //NOTE: Sorts by date paid or completed
                return new SelectList(from o in Orders.OrderByDescending(
                                       x => x.Value.date_paid ?? x.Value.date_completed)
                                      select new
                                      {
                                          o.Value.id,
                                          Value = String.Format("{0} - {1} - {2}",
                                          o.Value.id,
                                          String.Format(
                                              "{0:yyyy-MM-dd H:mm}",
                                               o.Value.date_paid ?? o.Value.date_created
                                          ),
                                          CountryUtils.GetEnglishName(o.Value.shipping.country)
                                          ),
                                      }, "id", "Value", CurrentId);
            }
        }

        public string CurrentId
        {
            get { return OrderRoute.OrderId; }
            set => OrderRoute.OrderId = value;
        }

        public string DateFrom => OrderRoute.DateFrom;
        public string DateTo => OrderRoute.DateTo;
        public Dictionary<string, WcOrder> Orders;
        public Dictionary<string, string> Errors { get; }
        public Dictionary<string, string> Warnings { get; }
        public IDictionary<string, string> OrderRouteData => new Dictionary<string, string> {
            {"orderId", CurrentId.ToString()},
            {"dateFrom", DateFrom},
            {"dateTo", DateTo},
        };
        public WcOrder Order
        {
            get
            {
                return Orders.FirstOrDefault(o => o.Key == CurrentId).Value;
            }
        }

        public OrderRouteModel OrderRoute { get; }

        public OrderViewModel(
            IList<WcOrder> orders,
            OrderRouteModel orderRoute,
            Dictionary<string, InvoiceAccrual> invoiceAccruals = null,
            Dictionary<string, string> errors = null,
            Dictionary<string, string> warnings = null)
        {
            Orders = orders.ToDictionary(item => item.id.ToString());
            Errors = errors ?? new Dictionary<string, string>();
            Warnings = warnings ?? new Dictionary<string, string>();
            OrderRoute = orderRoute;
            var orderId = orderRoute.OrderId;

            InvoiceAccruals = invoiceAccruals;

            OrderRoute.OrderId =
                orderId != null &&
                 Orders.ContainsKey(orderId)
                  ? orderId : orders.FirstOrDefault().id.ToString();
        }
        public WcOrder GetOrder()
        {
            return GetOrder(CurrentId);
        }

        public bool HasInvoice(string orderId)
        {
            var match = InvoiceAccruals.FirstOrDefault(o => o.Key == orderId);
            return match.Value != null;
        }
        public string GetWarning(string orderId)
        {
            return Warnings?.FirstOrDefault(o => o.Key == orderId).Value;
        }
        public string GetError(string orderId)
        {
            return Errors?.FirstOrDefault(o => o.Key == orderId).Value;
        }
        public InvoiceAccrual GetInvoice(string orderId)
        {
            return InvoiceAccruals?.FirstOrDefault(o => o.Key == orderId).Value;
        }
        public WcOrder GetOrder(string orderId = null)
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            return Orders?.FirstOrDefault(o => o.Key == orderId).Value;
        }
        public void SetCurrentId(string id)
        {
            OrderRoute.OrderId = id;
        }
    }
}