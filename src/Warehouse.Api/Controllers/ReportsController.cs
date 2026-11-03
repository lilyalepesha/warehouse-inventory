using Microsoft.AspNetCore.Mvc;
using Warehouse.Api.Contracts;
using Warehouse.Api.Services;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("profit")]
    public async Task<ActionResult<ProfitReportDto>> GetProfit(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] decimal? taxRate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periodTo = to ?? today;
        var periodFrom = from ?? new DateOnly(periodTo.Year, periodTo.Month, 1);

        return Ok(await _reports.GetProfitReportAsync(periodFrom, periodTo, taxRate));
    }

    [HttpGet("expired-write-offs")]
    public async Task<ActionResult<IReadOnlyList<ExpiredWriteOffDto>>> GetExpiredWriteOffs(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        return Ok(await _reports.GetExpiredWriteOffsAsync(from, to));
    }

    [HttpGet("soon-to-expire")]
    public async Task<ActionResult<IReadOnlyList<SoonToExpireDto>>> GetSoonToExpire(
        [FromQuery] int days = 7)
    {
        return Ok(await _reports.GetSoonToExpireAsync(days));
    }

    [HttpGet("stock")]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> GetStock()
    {
        return Ok(await _reports.GetStockAsync());
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        return Ok(await _reports.GetDashboardAsync());
    }
}
