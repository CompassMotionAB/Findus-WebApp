namespace Findus.Models {
    public class OrderRouteModel {
        public ulong? OrderId;
        public string DateFrom = null;
        public string DateTo = null;
        public OrderRouteModel(ulong? orderId, string dateFrom = null, string dateTo = null)
        {
            OrderId = orderId;
            DateFrom = dateFrom;
            DateTo = dateTo;
        }

        public bool HasDateRange() => !string.IsNullOrEmpty(DateFrom) && !string.IsNullOrEmpty(DateTo);
        public bool IsValid() => OrderId != null || HasDateRange();
    }
}