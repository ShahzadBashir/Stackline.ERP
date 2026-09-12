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
    public string MovementType { get; set; } = string.Empty; // PurchaseIn | SaleOut | AdjustmentIn | AdjustmentOut
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