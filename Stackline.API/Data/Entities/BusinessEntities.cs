namespace Stackline.API.Data.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
}

public class Item : BaseEntity
{
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string UnitOfMeasure { get; set; } = "Pcs";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int ReorderLevel { get; set; }
}

public class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public class StockLevel : BaseEntity
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class StockMovement : BaseEntity
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    
    // PurchaseIn | SaleOut | AdjustmentIn | AdjustmentOut | TransferIn | TransferOut
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
}

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public decimal OpeningBalance { get; set; }
}

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OpeningBalance { get; set; }
}

public class Purchase : BaseEntity
{
    public string PurchaseNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid WarehouseId { get; set; }

    public DateOnly PurchaseDate { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public decimal TotalAmount { get; set; }

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public ICollection<PurchaseLine> Lines { get; set; } =
        new List<PurchaseLine>();
}

public class PurchaseLine
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public Purchase Purchase { get; set; } = null!;

    public Guid ItemId { get; set; }

    public string SKU { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }
}

public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid WarehouseId { get; set; }

    public DateOnly SaleDate { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public decimal TotalAmount { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public ICollection<SaleLine> Lines { get; set; } =
        new List<SaleLine>();
}

public class SaleLine
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public Guid ItemId { get; set; }

    public string SKU { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public class CustomerReceipt : BaseEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "Cash";

    public string? PaymentReference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }
}

public class SupplierPayment : BaseEntity
{
    public string PaymentNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "Cash";

    public string? PaymentReference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }
}

public class StockAdjustment : BaseEntity
{
    public string AdjustmentNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public DateOnly AdjustmentDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public ICollection<StockAdjustmentLine> Lines { get; set; } =
        new List<StockAdjustmentLine>();
}

public class StockAdjustmentLine
{
    public Guid Id { get; set; }

    public Guid StockAdjustmentId { get; set; }

    public StockAdjustment StockAdjustment { get; set; } = null!;

    public Guid ItemId { get; set; }

    public string SKU { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
}

public class StockTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty;

    public Guid SourceWarehouseId { get; set; }

    public Guid DestinationWarehouseId { get; set; }

    public DateOnly TransferDate { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime? PostedAt { get; set; }

    public Guid? PostedBy { get; set; }

    public ICollection<StockTransferLine> Lines { get; set; } =
        new List<StockTransferLine>();
}

public class StockTransferLine
{
    public Guid Id { get; set; }

    public Guid StockTransferId { get; set; }

    public StockTransfer StockTransfer { get; set; } = null!;

    public Guid ItemId { get; set; }

    public string SKU { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
}