using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;

    public ReceiptsController(AppDbContext db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReceiptDto>>> GetAll(
        [FromQuery] int limit = 50,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = _db.Receipts
            .AsNoTracking()
            .Include(receipt => receipt.Supplier)
            .Include(receipt => receipt.Items)
                .ThenInclude(item => item.Product)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(receipt => receipt.ReceivedOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(receipt => receipt.ReceivedOn <= to.Value);
        }

        var receipts = await query
            .OrderByDescending(receipt => receipt.ReceivedOn)
            .ThenByDescending(receipt => receipt.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(receipts.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ReceiptDto>> Create(CreateReceiptRequest request)
    {
        var receipt = await _inventory.ReceiveAsync(request);
        var dto = ToDto(receipt);
        return Created($"/api/receipts/{receipt.Id}", dto);
    }

    internal static ReceiptDto ToDto(Domain.Receipt receipt)
    {
        var items = receipt.Items
            .OrderBy(item => item.Id)
            .Select(item => new ReceiptItemDto(
                item.Id,
                item.ProductId,
                item.Product.Sku,
                item.Product.Name,
                item.Product.Unit,
                item.Quantity,
                item.RemainingQuantity,
                item.UnitCost,
                item.ExpiryDate))
            .ToList();

        return new ReceiptDto(
            receipt.Id,
            receipt.Number,
            receipt.SupplierId,
            receipt.Supplier.Name,
            receipt.ReceivedOn,
            receipt.InvoiceNumber,
            Math.Round(receipt.TotalCost, 2, MidpointRounding.AwayFromZero),
            items);
    }
}
