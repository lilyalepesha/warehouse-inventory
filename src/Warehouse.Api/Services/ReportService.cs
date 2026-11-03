using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Domain;

namespace Warehouse.Api.Services;

/// <summary>
/// Аналитика: прибыль за период, просроченные списания, товары с истекающим сроком.
/// Отчёты строятся по снимкам себестоимости в строках списания, поэтому
/// изменение закупочных цен задним числом не искажает историю.
/// </summary>
public class ReportService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ReportService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public decimal DefaultTaxRate
    {
        get
        {
            var configured = _configuration.GetValue<decimal?>("Tax:Rate");
            return configured ?? 0.06m;
        }
    }

    public async Task<ProfitReportDto> GetProfitReportAsync(
        DateOnly from,
        DateOnly to,
        decimal? taxRate = null)
    {
        if (to < from)
        {
            throw new DomainException("Начало периода не может быть позже окончания.");
        }

        var rate = taxRate ?? DefaultTaxRate;
        if (rate < 0m || rate > 1m)
        {
            throw new DomainException("Ставка налога должна быть в диапазоне от 0 до 1 (например, 0.06 = 6%).");
        }

        var items = await _db.WriteOffItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item =>
                item.WriteOff.Reason == WriteOffReason.Sale &&
                item.WriteOff.OccurredOn >= from &&
                item.WriteOff.OccurredOn <= to)
            .ToListAsync();

        var lines = items
            .GroupBy(item => new
            {
                item.ProductId,
                item.Product.Sku,
                item.Product.Name,
                item.Product.Unit
            })
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity);
                var revenue = group.Sum(item => item.Quantity * (item.UnitSalePrice ?? 0m));
                var cost = group.Sum(item => item.Quantity * item.UnitCost);
                var gross = revenue - cost;
                var tax = Money(revenue * rate);

                return new ProfitLineDto(
                    group.Key.ProductId,
                    group.Key.Sku,
                    group.Key.Name,
                    group.Key.Unit,
                    quantity,
                    Money(revenue),
                    Money(cost),
                    Money(gross),
                    tax,
                    Money(gross - tax));
            })
            .OrderByDescending(line => line.NetProfit)
            .ToList();

        var totals = new ProfitTotalsDto(
            lines.Sum(line => line.QuantitySold),
            Money(lines.Sum(line => line.Revenue)),
            Money(lines.Sum(line => line.Cost)),
            Money(lines.Sum(line => line.GrossProfit)),
            Money(lines.Sum(line => line.Tax)),
            Money(lines.Sum(line => line.NetProfit)));

        return new ProfitReportDto(from, to, rate, lines, totals);
    }

    public async Task<IReadOnlyList<ExpiredWriteOffDto>> GetExpiredWriteOffsAsync(
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = _db.WriteOffItems
            .AsNoTracking()
            .Include(item => item.WriteOff)
            .Include(item => item.Product)
            .Include(item => item.ReceiptItem)
                .ThenInclude(batch => batch.Receipt)
                    .ThenInclude(receipt => receipt.Supplier)
            .Where(item => item.WriteOff.Reason == WriteOffReason.Expiry);

        if (from.HasValue)
        {
            query = query.Where(item => item.WriteOff.OccurredOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.WriteOff.OccurredOn <= to.Value);
        }

        var items = await query
            .OrderByDescending(item => item.WriteOff.OccurredOn)
            .ThenBy(item => item.Product.Name)
            .ToListAsync();

        return items.Select(item => new ExpiredWriteOffDto(
                item.WriteOffId,
                item.WriteOff.Number,
                item.WriteOff.OccurredOn,
                item.ProductId,
                item.Product.Sku,
                item.Product.Name,
                item.Product.Unit,
                item.Quantity,
                Money(item.UnitCost),
                Money(item.Quantity * item.UnitCost),
                item.ExpiryDate,
                item.ReceiptItem.Receipt.Supplier.Name,
                item.WriteOff.Comment))
            .ToList();
    }

    public async Task<IReadOnlyList<SoonToExpireDto>> GetSoonToExpireAsync(int days = 7)
    {
        if (days < 0 || days > 365)
        {
            throw new DomainException("Горизонт прогноза должен быть от 0 до 365 дней.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var horizon = today.AddDays(days);

        var batches = await _db.ReceiptItems
            .AsNoTracking()
            .Include(batch => batch.Product)
            .Include(batch => batch.Receipt)
                .ThenInclude(receipt => receipt.Supplier)
            .Where(batch =>
                batch.RemainingQuantity > 0 &&
                batch.ExpiryDate != null &&
                batch.ExpiryDate >= today &&
                batch.ExpiryDate <= horizon)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.Product.Name)
            .ToListAsync();

        return batches.Select(batch => new SoonToExpireDto(
                batch.Id,
                batch.ProductId,
                batch.Product.Sku,
                batch.Product.Name,
                batch.Product.Unit,
                batch.RemainingQuantity,
                Money(batch.UnitCost),
                batch.ExpiryDate!.Value,
                batch.ExpiryDate.Value.DayNumber - today.DayNumber,
                batch.Receipt.Supplier.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<StockItemDto>> GetStockAsync()
    {
        var batches = await _db.ReceiptItems
            .AsNoTracking()
            .Include(batch => batch.Product)
            .Where(batch => batch.RemainingQuantity > 0)
            .ToListAsync();

        return batches
            .GroupBy(batch => new
            {
                batch.ProductId,
                batch.Product.Sku,
                batch.Product.Name,
                batch.Product.Unit,
                batch.Product.Category
            })
            .Select(group => new StockItemDto(
                group.Key.ProductId,
                group.Key.Sku,
                group.Key.Name,
                group.Key.Unit,
                group.Key.Category,
                group.Sum(batch => batch.RemainingQuantity),
                Money(group.Sum(batch => batch.RemainingQuantity * batch.UnitCost)),
                group.Where(batch => batch.ExpiryDate != null)
                    .Min(batch => batch.ExpiryDate),
                group.Count()))
            .OrderBy(item => item.Name)
            .ToList();
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var stock = await GetStockAsync();
        var soon = await GetSoonToExpireAsync(7);
        var profit = await GetProfitReportAsync(today.AddDays(-29), today);

        var expiredItems = await _db.WriteOffItems
            .AsNoTracking()
            .Where(item => item.WriteOff.Reason == WriteOffReason.Expiry)
            .Select(item => new { item.Quantity, item.UnitCost })
            .ToListAsync();

        var expiredLoss = Money(expiredItems.Sum(item => item.Quantity * item.UnitCost));

        return new DashboardDto(
            stock.Sum(item => item.Quantity),
            Money(stock.Sum(item => item.StockValue)),
            stock.Count,
            soon.Count,
            expiredLoss,
            profit.Totals.Revenue,
            profit.Totals.GrossProfit,
            profit.Totals.Tax,
            profit.Totals.NetProfit,
            profit.TaxRate);
    }

    private static decimal Money(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
