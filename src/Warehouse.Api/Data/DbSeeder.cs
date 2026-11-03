using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Domain;

namespace Warehouse.Api.Data;

/// <summary>
/// Демонстрационные данные: несколько поставщиков, партии с разной
/// закупочной ценой, продажи, просроченные списания и товары,
/// у которых срок годности истекает в ближайшие дни.
/// </summary>
public class DbSeeder
{
    private readonly AppDbContext _db;

    public DbSeeder(AppDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        if (await _db.Products.AnyAsync())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        var dairy = new Supplier
        {
            Name = "ООО «Молочный двор»",
            Inn = "7701234567",
            ContactPerson = "Иванова Анна",
            Phone = "+7 (495) 120-40-10"
        };
        var bakery = new Supplier
        {
            Name = "АО «Свежий хлеб»",
            Inn = "7802345678",
            ContactPerson = "Петров Сергей",
            Phone = "+7 (812) 330-18-22"
        };
        var farm = new Supplier
        {
            Name = "ООО «ГринФуд»",
            Inn = "5003456789",
            ContactPerson = "Соколова Мария",
            Phone = "+7 (916) 555-72-03"
        };
        var drinks = new Supplier
        {
            Name = "ООО «Напитки Плюс»",
            Inn = "6604567890",
            ContactPerson = "Кузнецов Илья",
            Phone = "+7 (343) 210-90-45"
        };

        _db.Suppliers.AddRange(dairy, bakery, farm, drinks);
        await _db.SaveChangesAsync();

        var milk = new Product { Sku = "MLK-001", Name = "Молоко 3,2%, 1 л", Unit = "шт", Category = "Молочные продукты" };
        var yogurt = new Product { Sku = "YOG-001", Name = "Йогурт натуральный, 300 г", Unit = "шт", Category = "Молочные продукты" };
        var cheese = new Product { Sku = "CHS-001", Name = "Сыр «Российский», 200 г", Unit = "шт", Category = "Молочные продукты" };
        var bread = new Product { Sku = "BRD-001", Name = "Хлеб «Бородинский», 400 г", Unit = "шт", Category = "Хлеб" };
        var apples = new Product { Sku = "APL-001", Name = "Яблоки «Гала»", Unit = "кг", Category = "Овощи и фрукты" };
        var chicken = new Product { Sku = "CHK-001", Name = "Филе куриное", Unit = "кг", Category = "Мясо" };
        var juice = new Product { Sku = "JUC-001", Name = "Сок апельсиновый, 1 л", Unit = "шт", Category = "Напитки" };
        var chocolate = new Product { Sku = "CHO-001", Name = "Шоколад горький, 90 г", Unit = "шт", Category = "Кондитерские изделия" };

        _db.Products.AddRange(milk, yogurt, cheese, bread, apples, chicken, juice, chocolate);
        await _db.SaveChangesAsync();

        var receiptSequence = 0;
        var writeOffSequence = 0;
        var receipts = new List<Receipt>();

        ReceiptItem Batch(Product product, decimal quantity, decimal unitCost, DateOnly expiry)
            => new()
            {
                ProductId = product.Id,
                Product = product,
                Quantity = quantity,
                RemainingQuantity = quantity,
                UnitCost = unitCost,
                ExpiryDate = expiry
            };

        Receipt AddReceipt(Supplier supplier, DateOnly receivedOn, string invoiceNumber, params ReceiptItem[] items)
        {
            var receipt = new Receipt
            {
                Number = $"ПОС-{++receiptSequence:0000}",
                SupplierId = supplier.Id,
                Supplier = supplier,
                ReceivedOn = receivedOn,
                InvoiceNumber = invoiceNumber
            };

            foreach (var item in items)
            {
                receipt.Items.Add(item);
            }

            receipts.Add(receipt);
            return receipt;
        }

        var a1 = Batch(milk, 100m, 52m, today.AddDays(-53));
        var a2 = Batch(yogurt, 60m, 38m, today.AddDays(-50));
        AddReceipt(dairy, today.AddDays(-60), "МД-1042", a1, a2);

        var b1 = Batch(bread, 80m, 28m, today.AddDays(-42));
        AddReceipt(bakery, today.AddDays(-45), "ХЛ-2210", b1);

        var b2 = Batch(cheese, 40m, 180m, today.AddDays(-15));
        AddReceipt(dairy, today.AddDays(-45), "МД-1078", b2);

        var c1 = Batch(milk, 150m, 58m, today.AddDays(-23));
        AddReceipt(dairy, today.AddDays(-30), "МД-1121", c1);

        var c2 = Batch(apples, 200m, 60m, today.AddDays(-16));
        var c3 = Batch(chicken, 80m, 240m, today.AddDays(-25));
        AddReceipt(farm, today.AddDays(-30), "ГФ-8803", c2, c3);

        var d1 = Batch(juice, 90m, 70m, today.AddDays(160));
        var d2 = Batch(chocolate, 120m, 90m, today.AddDays(345));
        AddReceipt(drinks, today.AddDays(-20), "НП-5501", d1, d2);

        var e1 = Batch(milk, 80m, 60m, today.AddDays(-5));
        AddReceipt(dairy, today.AddDays(-12), "МД-1184", e1);

        var e2 = Batch(bread, 100m, 30m, today.AddDays(-9));
        AddReceipt(bakery, today.AddDays(-12), "ХЛ-2298", e2);

        var f1 = Batch(yogurt, 80m, 42m, today.AddDays(5));
        var f4 = Batch(cheese, 30m, 195m, today.AddDays(25));
        AddReceipt(dairy, today.AddDays(-5), "МД-1240", f1, f4);

        var f2 = Batch(apples, 100m, 65m, today.AddDays(9));
        var f3 = Batch(chicken, 50m, 250m, today.AddDays(4));
        AddReceipt(farm, today.AddDays(-5), "ГФ-8891", f2, f3);

        var g1 = Batch(milk, 120m, 61m, today.AddDays(5));
        AddReceipt(dairy, today.AddDays(-2), "МД-1277", g1);

        var g2 = Batch(bread, 60m, 32m, today.AddDays(1));
        AddReceipt(bakery, today.AddDays(-2), "ХЛ-2320", g2);

        var g3 = Batch(juice, 60m, 72m, today.AddDays(180));
        var g4 = Batch(chocolate, 50m, 95m, today.AddDays(350));
        AddReceipt(drinks, today.AddDays(-2), "НП-5570", g3, g4);

        var h1 = Batch(yogurt, 40m, 44m, today.AddDays(10));
        AddReceipt(dairy, today, "МД-1301", h1);

        var h2 = Batch(apples, 50m, 68m, today.AddDays(14));
        AddReceipt(farm, today, "ГФ-8912", h2);

        _db.Receipts.AddRange(receipts);
        await _db.SaveChangesAsync();

        void AddWriteOff(
            DateOnly occurredOn,
            WriteOffReason reason,
            string? comment,
            params (ReceiptItem Batch, decimal Quantity, decimal? SalePrice)[] lines)
        {
            var writeOff = new WriteOff
            {
                Number = $"СП-{++writeOffSequence:0000}",
                OccurredOn = occurredOn,
                Reason = reason,
                Comment = comment
            };

            foreach (var (batch, quantity, salePrice) in lines)
            {
                if (batch.RemainingQuantity < quantity)
                {
                    throw new InvalidOperationException(
                        $"Некорректные демо-данные: в партии {batch.Product.Name} остаток {batch.RemainingQuantity}, запрошено {quantity}.");
                }

                batch.RemainingQuantity -= quantity;
                writeOff.Items.Add(new WriteOffItem
                {
                    ReceiptItemId = batch.Id,
                    ReceiptItem = batch,
                    ProductId = batch.ProductId,
                    Product = batch.Product,
                    Quantity = quantity,
                    UnitCost = batch.UnitCost,
                    UnitSalePrice = reason == WriteOffReason.Sale ? salePrice : null,
                    ExpiryDate = batch.ExpiryDate
                });
            }

            _db.WriteOffs.Add(writeOff);
        }

        // Списания по истечении срока годности.
        AddWriteOff(today.AddDays(-49), WriteOffReason.Expiry, "Утилизация просроченной партии",
            (a1, 70m, null), (a2, 40m, null));
        AddWriteOff(today.AddDays(-45), WriteOffReason.Expiry, "Утилизация просроченной партии",
            (a1, 30m, null), (a2, 20m, null), (b1, 40m, null));
        AddWriteOff(today.AddDays(-41), WriteOffReason.Expiry, "Истёк срок годности",
            (b1, 40m, null));
        AddWriteOff(today.AddDays(-22), WriteOffReason.Expiry, "Истёк срок годности",
            (c1, 100m, null), (c3, 40m, null));
        AddWriteOff(today.AddDays(-14), WriteOffReason.Expiry, "Истёк срок годности",
            (c2, 120m, null), (b2, 20m, null));
        AddWriteOff(today.AddDays(-10), WriteOffReason.Expiry, "Истёк срок годности",
            (c1, 50m, null), (c2, 40m, null), (c3, 40m, null), (b2, 20m, null));
        AddWriteOff(today.AddDays(-4), WriteOffReason.Expiry, "Истёк срок годности",
            (e1, 70m, null), (e2, 100m, null));
        AddWriteOff(today.AddDays(-1), WriteOffReason.Expiry, "Истёк срок годности",
            (e1, 10m, null), (c2, 40m, null));

        // Продажи за последние 25 дней.
        AddWriteOff(today.AddDays(-25), WriteOffReason.Sale, "Розничная продажа",
            (g1, 25m, 95m), (g2, 15m, 49m), (f2, 25m, 115m), (f3, 15m, 410m));
        AddWriteOff(today.AddDays(-18), WriteOffReason.Sale, "Розничная продажа",
            (g1, 20m, 92m), (f4, 10m, 335m), (d1, 20m, 120m), (d2, 20m, 160m));
        AddWriteOff(today.AddDays(-10), WriteOffReason.Sale, "Розничная продажа",
            (g1, 15m, 98m), (f1, 20m, 75m), (f2, 25m, 115m), (g2, 15m, 49m));
        AddWriteOff(today.AddDays(-3), WriteOffReason.Sale, "Розничная продажа",
            (f3, 15m, 410m), (f4, 5m, 335m), (d1, 25m, 120m), (d2, 25m, 160m), (f1, 15m, 79m));
        AddWriteOff(today.AddDays(-1), WriteOffReason.Sale, "Розничная продажа",
            (f2, 25m, 115m), (g3, 15m, 120m), (g4, 10m, 165m));

        // Списание порчи для демонстрации третьей причины.
        AddWriteOff(today.AddDays(-7), WriteOffReason.Damage, "Бой при выкладке",
            (g2, 5m, null));

        await _db.SaveChangesAsync();
    }
}
