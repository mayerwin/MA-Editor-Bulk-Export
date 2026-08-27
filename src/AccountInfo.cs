using System;

namespace MAEditorBulkExport
{
    public class AccountInfo
    {
        public long StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public long Status { get; set; } = 31; // Default to Active (31)
        public string DatabasePath { get; set; } = string.Empty;
        public bool DatabaseExists { get; set; }

        public override string ToString() => $"{AccountNumber} - {AccountName} (ID: {StoreId})";
    }
}
