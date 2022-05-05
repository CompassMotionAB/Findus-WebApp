namespace Findus.Models {
    public class OrderRouteModel {
        public string OrderId;
        public string DateFrom = null;
        public string DateTo = null;
        public string Status = null;
        public OrderRouteModel(string orderId, string dateFrom = null, string dateTo = null, string status = "completed")
        {
            OrderId = orderId;
            DateFrom = dateFrom;
            DateTo = dateTo;
            Status = status;
        }

        public bool HasDateRange() => !string.IsNullOrEmpty(DateFrom) && !string.IsNullOrEmpty(DateTo);
        public bool IsValid() => OrderId != null || HasDateRange();
    }
}