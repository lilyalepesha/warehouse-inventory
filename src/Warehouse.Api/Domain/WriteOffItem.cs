namespace Warehouse.Api.Domain;

/// <summary>
/// Строка списания. Хранит снимок себестоимости партии на момент операции,
/// поэтому отчёты не зависят от последующих изменений цен.
/// </summary>
public class WriteOffItem
{
    public int Id { get; set; }

    public int WriteOffId { get; set; }
    public WriteOff WriteOff { get; set; } = null!;

    public int ReceiptItemId { get; set; }
    public ReceiptItem ReceiptItem { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal? UnitSalePrice { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public decimal Cost => Quantity * UnitCost;
    public decimal Revenue => Quantity * (UnitSalePrice ?? 0m);
}
