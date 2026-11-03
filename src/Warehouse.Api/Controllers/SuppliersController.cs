using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;

    public SuppliersController(AppDbContext db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> GetAll()
    {
        var suppliers = await _db.Suppliers
            .AsNoTracking()
            .OrderBy(supplier => supplier.Name)
            .Select(supplier => new SupplierDto(
                supplier.Id,
                supplier.Name,
                supplier.Inn,
                supplier.ContactPerson,
                supplier.Phone,
                supplier.Receipts.Count))
            .ToListAsync();

        return Ok(suppliers);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(CreateSupplierRequest request)
    {
        var supplier = await _inventory.CreateSupplierAsync(request);
        var dto = new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Inn,
            supplier.ContactPerson,
            supplier.Phone,
            0);

        return Created($"/api/suppliers/{supplier.Id}", dto);
    }
}
