using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Domain;
using Warehouse.Api.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/write-offs")]
public class WriteOffsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;

    public WriteOffsController(AppDbContext db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WriteOffDto>>> GetAll(
        [FromQuery] int limit = 50,
        [FromQuery] WriteOffReason? reason = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = _db.WriteOffs
            .AsNoTracking()
            .Include(writeOff => writeOff.Items)
                .ThenInclude(item => item.Product)
            .AsQueryable();

        if (reason.HasValue)
        {
            query = query.Where(writeOff => writeOff.Reason == reason.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(writeOff => writeOff.OccurredOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(writeOff => writeOff.OccurredOn <= to.Value);
        }

        var writeOffs = await query
            .OrderByDescending(writeOff => writeOff.OccurredOn)
            .ThenByDescending(writeOff => writeOff.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(writeOffs.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<WriteOffDto>> Create(CreateWriteOffRequest request)
    {
        var writeOff = await _inventory.WriteOffAsync(request);
        var dto = ToDto(writeOff);
        return Created($"/api/write-offs/{writeOff.Id}", dto);
    }

    internal static WriteOffDto ToDto(WriteOff writeOff)
    {
        var items = writeOff.Items
            .OrderBy(item => item.Id)
            .Select(item => new WriteOffItemDto(
                item.Id,
                item.ProductId,
                item.Product.Sku,
                item.Product.Name,
                item.Product.Unit,
                item.Quantity,
                item.UnitCost,
                item.UnitSalePrice,
                Math.Round(item.Quantity * item.UnitCost, 2, MidpointRounding.AwayFromZero),
                item.UnitSalePrice.HasValue
                    ? Math.Round(item.Quantity * item.UnitSalePrice.Value, 2, MidpointRounding.AwayFromZero)
                    : null,
                item.ExpiryDate))
            .ToList();

        return new WriteOffDto(
            writeOff.Id,
            writeOff.Number,
            writeOff.OccurredOn,
            writeOff.Reason,
            writeOff.Comment,
            Math.Round(writeOff.TotalCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(writeOff.TotalRevenue, 2, MidpointRounding.AwayFromZero),
            items);
    }
}
