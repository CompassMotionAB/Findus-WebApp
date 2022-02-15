using System.Collections.Generic;
using Fortnox.SDK.Entities;

namespace Findus.Helpers {
    public static class InvoiceAccrualExtensions {
        public static void AddRow(this InvoiceAccrual invoice, int accountNr, decimal debit = 0, decimal credit = 0) {
            (invoice.InvoiceAccrualRows ??= new List<InvoiceAccrualRow>()).Add(
                new InvoiceAccrualRow {
                    Account = accountNr,
                    Debit = debit,
                    Credit = credit,
                }
            );
        }
    }
}