using System;
using Order = WooCommerceNET.WooCommerce.v2.Order;

namespace Findus.Helpers
{
    public static class OrderExtensions
    {
        public static bool HasDocumentLink(this Order order)
        {
            return !string.IsNullOrEmpty(order.order_key)
                || !string.IsNullOrEmpty(
                    order.meta_data.Find(m => m.key == "_wcpdf_document_link")?.value as string
                )
                || !string.IsNullOrEmpty(
                    order.meta_data.Find(m => m.key == "_wc_order_key")?.value as string
                );
        }

        public static string TryGetDocumentLink(this Order order)
        {
            string pdfLink =
                order.meta_data.Find(m => m.key == "_wcpdf_document_link")?.value as string;

            if (string.IsNullOrEmpty(pdfLink))
            {
                string orderKey =
                    order.meta_data.Find(m => m.key == "_wc_order_key")?.value as string ?? order.order_key;

                string orderId = order.id.ToString();
                if (string.IsNullOrEmpty(orderKey))
                {
                    throw new System.Exception(
                        $"Order: ${orderId} is missing document_link and order_key"
                    );
                }
                else
                {
                    pdfLink =
                        $"https://gamerbulk.com/wp-admin/admin-ajax.php?action=generate_wpo_wcpdf&template_type=invoice&order_ids={orderId}&order_key={orderKey}";
                }
            }

            return pdfLink;
        }

        public static long GetInvoiceReference(this Order order)
        {
            return Convert.ToInt64(
                (string)(order.meta_data.Find(m => m.key == "_fortnox_invoice_number")?.value)
            );
        }
    }
}
