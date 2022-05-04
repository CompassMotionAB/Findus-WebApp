using System;
using WooCommerceNET.WooCommerce.v2;

namespace Findus.Helpers
{
    public static class OrderLineItemExtensions
    {
        public static bool HasReducedRate(this OrderLineItem item)
        {
            if (
                item.tax_class == "reduced-rate"
                || item.tax_class == "normal-rate"
                || string.IsNullOrEmpty(item.tax_class)
            )
            {
                throw new Exception(
                    "Tax Class in Items of Order are only expected to have either 'normal-rate', 'reduced-rate' or empty ( normal rate )"
                );
            }
            return item.tax_class == "reduced-rate";
        }

        public static string SanitizeNameForFortnox(this OrderLineItem item)
        {
            return item.name.SanitizeStringForFortnox();
        }
    }
}
