using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using WcOrder = WooCommerceNET.WooCommerce.v2.Order;

namespace FindusWebApp.Models
{
    public class OrderViewModel
    {

        private static string GetEnglishName(string countryCode)
        {
            try
            {
                return new RegionInfo(countryCode).EnglishName;
            }
            catch (Exception)
            {
                return String.Format("Missing English Name: {0}", countryCode);
            }
        }

        public IEnumerable<SelectListItem> OrderSelect
        {
            get
            {
                return new SelectList((from o in Orders.OrderByDescending(
                                       //NOTE: Sort by date paid/completed
                                       x => x.Value.date_paid ?? x.Value.date_completed)
                                       select new
                                       {
                                           o.Value.id,
                                           Value = String.Format("{0} - {1} - {2}",
                                           o.Value.id,
                                           String.Format(
                                               "{0:yyyy/M/d hh:mm}",
                                                o.Value.date_paid ?? o.Value.date_created
                                           ),

                                           GetEnglishName(o.Value.shipping.country)
                                           ),
                                       }), "id", "Value", CurrentId);

            }
        }


        public ulong? CurrentId { get; set; }

        public Dictionary<ulong?, WcOrder> Orders;
        public WcOrder Order
        {
            get
            {
                return Orders.FirstOrDefault(o => o.Key == CurrentId).Value;
            }
        }

        public OrderViewModel(IList<WcOrder> orders, ulong? currentId = null)
        {
            Orders = orders.ToDictionary(item => item.id);
            CurrentId =
                currentId != null &&
                 Orders.ContainsKey(currentId)
                  ? currentId : orders.FirstOrDefault().id;
        }
        public WcOrder GetOrder()
        {
            return Orders.FirstOrDefault(o => o.Key == CurrentId).Value;
        }

        public WcOrder GetOrder(ulong? orderId = null)
        {
            if (orderId == null) return null;
            return Orders.FirstOrDefault(o => o.Key == orderId).Value;
        }
        public WcOrder GetOrder(string orderId = null)
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            return Orders.FirstOrDefault(o => o.Key == ulong.Parse(orderId)).Value;
        }

        public void SetCurrentId(ulong? id)
        {
            CurrentId = id;
        }
    }
}