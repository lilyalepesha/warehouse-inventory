namespace Warehouse.Api.Domain;

/// <summary>Номенклатурная позиция магазина.</summary>
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();
    public ICollection<WriteOffItem> WriteOffItems { get; set; } = new List<WriteOffItem>();
}
