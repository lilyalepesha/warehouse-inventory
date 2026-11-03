namespace Warehouse.Api.Domain;

/// <summary>Документ списания товара (продажа, порча, истечение срока).</summary>
public class WriteOff
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; }
    public WriteOffReason Reason { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WriteOffItem> Items { get; set; } = new List<WriteOffItem>();

    public decimal TotalCost => Items.Sum(item => item.Quantity * item.UnitCost);
    public decimal TotalRevenue => Items.Sum(item => item.Quantity * (item.UnitSalePrice ?? 0m));
}
