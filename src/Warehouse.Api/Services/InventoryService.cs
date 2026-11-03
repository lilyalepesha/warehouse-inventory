using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Domain;

namespace Warehouse.Api.Services;

/// <summary>
/// Команды складского учёта: приёмка, списание, создание справочников.
/// Здесь сосредоточены бизнес-правила и работа с партиями по FEFO/FIFO.
/// </summary>
public class InventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Supplier> CreateSupplierAsync(CreateSupplierRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Наименование поставщика обязательно.");
        }

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Inn = Normalize(request.Inn),
            ContactPerson = Normalize(request.ContactPerson),
            Phone = Normalize(request.Phone)
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return supplier;
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new DomainException("Артикул обязателен.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Наименование товара обязательно.");
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(product => product.Sku == sku))
        {
            throw new DomainException($"Товар с артикулом «{sku}» уже существует.", StatusCodes.Status409Conflict);
        }

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "шт" : request.Unit.Trim(),
            Category = Normalize(request.Category)
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Receipt> ReceiveAsync(CreateReceiptRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new DomainException("Поступление должно содержать хотя бы одну строку.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.ReceivedOn > today)
        {
            throw new DomainException("Дата поступления не может быть в будущем.");
        }

        var supplier = await _db.Suppliers.FindAsync(request.SupplierId)
            ?? throw new DomainException("Поставщик не найден.", StatusCodes.Status404NotFound);

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        var receipt = new Receipt
        {
            Number = await NextNumberAsync("ПОС", request.ReceivedOn),
            SupplierId = supplier.Id,
            Supplier = supplier,
            ReceivedOn = request.ReceivedOn,
            InvoiceNumber = Normalize(request.InvoiceNumber)
        };

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new DomainException($"Товар с идентификатором {item.ProductId} не найден.", StatusCodes.Status404NotFound);
            }

            if (item.Quantity <= 0)
            {
                throw new DomainException($"Количество товара «{product.Name}» должно быть больше нуля.");
            }

            if (item.UnitCost < 0)
            {
                throw new DomainException($"Закупочная цена товара «{product.Name}» не может быть отрицательной.");
            }

            if (item.ExpiryDate.HasValue && item.ExpiryDate.Value < request.ReceivedOn)
            {
                throw new DomainException($"Срок годности товара «{product.Name}» не может истечь раньше даты поступления.");
            }

            receipt.Items.Add(new ReceiptItem
            {
                ProductId = product.Id,
                Product = product,
                Quantity = item.Quantity,
                RemainingQuantity = item.Quantity,
                UnitCost = item.UnitCost,
                ExpiryDate = item.ExpiryDate
            });
        }

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    public async Task<WriteOff> WriteOffAsync(CreateWriteOffRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new DomainException("Списание должно содержать хотя бы одну строку.");
        }

        if (!Enum.IsDefined(request.Reason))
        {
            throw new DomainException("Неизвестная причина списания.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.OccurredOn > today)
        {
            throw new DomainException("Дата списания не может быть в будущем.");
        }

        var duplicateProduct = request.Items
            .GroupBy(item => item.ProductId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateProduct is not null)
        {
            throw new DomainException("Товар указан в списании несколько раз. Объедините строки.");
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        var writeOff = new WriteOff
        {
            Number = await NextNumberAsync("СП", request.OccurredOn),
            OccurredOn = request.OccurredOn,
            Reason = request.Reason,
            Comment = Normalize(request.Comment)
        };

        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new DomainException($"Товар с идентификатором {line.ProductId} не найден.", StatusCodes.Status404NotFound);
            }

            if (line.Quantity <= 0)
            {
                throw new DomainException($"Количество товара «{product.Name}» должно быть больше нуля.");
            }

            if (request.Reason == WriteOffReason.Sale && (!line.UnitSalePrice.HasValue || line.UnitSalePrice.Value <= 0))
            {
                throw new DomainException($"Для продажи товара «{product.Name}» укажите цену реализации больше нуля.");
            }

            var batchesQuery = _db.ReceiptItems
                .Where(batch => batch.ProductId == line.ProductId && batch.RemainingQuantity > 0);

            if (request.Reason == WriteOffReason.Sale)
            {
                // Продавать можно только непросроченный товар.
                batchesQuery = batchesQuery.Where(batch =>
                    batch.ExpiryDate == null || batch.ExpiryDate >= request.OccurredOn);
            }
            else if (request.Reason == WriteOffReason.Expiry)
            {
                // По причине истечения срока списываем только просроченные партии.
                batchesQuery = batchesQuery.Where(batch =>
                    batch.ExpiryDate != null && batch.ExpiryDate <= request.OccurredOn);
            }

            // FEFO: сначала истекает срок годности; внутри срока — более раннее поступление.
            var batches = await batchesQuery
                .OrderBy(batch => batch.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(batch => batch.Receipt.ReceivedOn)
                .ThenBy(batch => batch.Id)
                .ToListAsync();

            var quantityLeft = line.Quantity;

            foreach (var batch in batches)
            {
                if (quantityLeft <= 0)
                {
                    break;
                }

                var quantityToTake = Math.Min(quantityLeft, batch.RemainingQuantity);
                batch.RemainingQuantity -= quantityToTake;

                writeOff.Items.Add(new WriteOffItem
                {
                    ReceiptItemId = batch.Id,
                    ReceiptItem = batch,
                    ProductId = product.Id,
                    Product = product,
                    Quantity = quantityToTake,
                    UnitCost = batch.UnitCost,
                    UnitSalePrice = request.Reason == WriteOffReason.Sale ? line.UnitSalePrice : null,
                    ExpiryDate = batch.ExpiryDate
                });

                quantityLeft -= quantityToTake;
            }

            if (quantityLeft > 0)
            {
                var reasonText = request.Reason == WriteOffReason.Expiry
                    ? "просроченного товара"
                    : "товара на складе";
                throw new DomainException(
                    $"Недостаточно {reasonText} «{product.Name}»: запрошено {line.Quantity:0.###}, доступно {line.Quantity - quantityLeft:0.###}.");
            }
        }

        _db.WriteOffs.Add(writeOff);
        await _db.SaveChangesAsync();
        return writeOff;
    }

    private async Task<string> NextNumberAsync(string prefix, DateOnly date)
    {
        var stem = $"{prefix}-{date:yyyyMMdd}-";

        var count = prefix == "ПОС"
            ? await _db.Receipts.CountAsync(receipt => receipt.Number.StartsWith(stem))
            : await _db.WriteOffs.CountAsync(writeOff => writeOff.Number.StartsWith(stem));

        return $"{stem}{count + 1:000}";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
