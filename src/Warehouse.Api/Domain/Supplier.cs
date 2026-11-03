namespace Warehouse.Api.Domain;

/// <summary>Поставщик товара.</summary>
public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }

    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}
