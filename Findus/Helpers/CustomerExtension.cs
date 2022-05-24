using Fortnox.SDK.Entities;

namespace Findus.Helpers
{
    public static class CustomerExtension
    {
        public static Customer AddVatType(this Customer customer, string countryIso)
        {
            if (VerificationUtils.IsInsideEU(countryIso))
            {
                customer.VATType = CustomerVATType.EU_VAT;
            }
            else
            {
                customer.VATType = CustomerVATType.Export;
            }
            return customer;
        }
    }
}
