using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Api.Contracts;
using Warehouse.Api.Data;
using Warehouse.Api.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inventory;

    public ProductsController(AppDbContext db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll()
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .ToListAsync();

        var batches = await _db.ReceiptItems
            .AsNoTracking()
            .Where(batch => batch.RemainingQuantity > 0)
            .Select(batch => new
            {
                batch.ProductId,
                batch.RemainingQuantity,
                batch.UnitCost
            })
            .ToListAsync();

        var stock = batches
            .GroupBy(batch => batch.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Quantity = group.Sum(batch => batch.RemainingQuantity),
                    Value = group.Sum(batch => batch.RemainingQuantity * batch.UnitCost)
                });

        var result = products.Select(product =>
        {
            stock.TryGetValue(product.Id, out var productStock);
            return new ProductDto(
                product.Id,
                product.Sku,
                product.Name,
                product.Unit,
                product.Category,
                product.IsActive,
                productStock?.Quantity ?? 0m,
                Math.Round(productStock?.Value ?? 0m, 2, MidpointRounding.AwayFromZero));
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request)
    {
        var product = await _inventory.CreateProductAsync(request);
        var dto = new ProductDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Unit,
            product.Category,
            product.IsActive,
            0m,
            0m);

        return Created($"/api/products/{product.Id}", dto);
    }
}
