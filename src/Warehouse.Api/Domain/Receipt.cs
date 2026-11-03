namespace Warehouse.Api.Domain;

/// <summary>Документ поступления товара от поставщика.</summary>
public class Receipt
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateOnly ReceivedOn { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();

    public decimal TotalCost => Items.Sum(item => item.Quantity * item.UnitCost);
}
