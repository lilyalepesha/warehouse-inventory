namespace Warehouse.Api.Domain;

/// <summary>
/// Партия товара, полученная по конкретному поступлению.
/// Хранит закупочную цену и остаток, поэтому одна и та же позиция
/// может иметь разную себестоимость в разных партиях.
/// </summary>
public class ReceiptItem
{
    public int Id { get; set; }

    public int ReceiptId { get; set; }
    public Receipt Receipt { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public ICollection<WriteOffItem> WriteOffItems { get; set; } = new List<WriteOffItem>();
}
