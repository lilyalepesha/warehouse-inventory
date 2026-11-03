using Warehouse.Api.Domain;

namespace Warehouse.Api.Contracts;

// ---------- Справочники ----------

public sealed record SupplierDto(
    int Id,
    string Name,
    string? Inn,
    string? ContactPerson,
    string? Phone,
    int ReceiptCount);

public sealed record CreateSupplierRequest(
    string Name,
    string? Inn,
    string? ContactPerson,
    string? Phone);

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Unit,
    string? Category,
    bool IsActive,
    decimal StockQuantity,
    decimal StockValue);

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Unit,
    string? Category);

// ---------- Поступления ----------

public sealed record CreateReceiptItemRequest(
    int ProductId,
    decimal Quantity,
    decimal UnitCost,
    DateOnly? ExpiryDate);

public sealed record CreateReceiptRequest(
    int SupplierId,
    DateOnly ReceivedOn,
    string? InvoiceNumber,
    List<CreateReceiptItemRequest> Items);

public sealed record ReceiptItemDto(
    int Id,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateOnly? ExpiryDate);

public sealed record ReceiptDto(
    int Id,
    string Number,
    int SupplierId,
    string SupplierName,
    DateOnly ReceivedOn,
    string? InvoiceNumber,
    decimal TotalCost,
    IReadOnlyList<ReceiptItemDto> Items);

// ---------- Списания ----------

public sealed record CreateWriteOffItemRequest(
    int ProductId,
    decimal Quantity,
    decimal? UnitSalePrice);

public sealed record CreateWriteOffRequest(
    DateOnly OccurredOn,
    WriteOffReason Reason,
    string? Comment,
    List<CreateWriteOffItemRequest> Items);

public sealed record WriteOffItemDto(
    int Id,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    decimal? UnitSalePrice,
    decimal Cost,
    decimal? Revenue,
    DateOnly? ExpiryDate);

public sealed record WriteOffDto(
    int Id,
    string Number,
    DateOnly OccurredOn,
    WriteOffReason Reason,
    string? Comment,
    decimal TotalCost,
    decimal TotalRevenue,
    IReadOnlyList<WriteOffItemDto> Items);

// ---------- Отчёты ----------

public sealed record ProfitLineDto(
    int ProductId,
    string Sku,
    string Name,
    string Unit,
    decimal QuantitySold,
    decimal Revenue,
    decimal Cost,
    decimal GrossProfit,
    decimal Tax,
    decimal NetProfit);

public sealed record ProfitTotalsDto(
    decimal QuantitySold,
    decimal Revenue,
    decimal Cost,
    decimal GrossProfit,
    decimal Tax,
    decimal NetProfit);

public sealed record ProfitReportDto(
    DateOnly From,
    DateOnly To,
    decimal TaxRate,
    IReadOnlyList<ProfitLineDto> Lines,
    ProfitTotalsDto Totals);

public sealed record ExpiredWriteOffDto(
    int WriteOffId,
    string Number,
    DateOnly OccurredOn,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    decimal LossAmount,
    DateOnly? ExpiryDate,
    string SupplierName,
    string? Comment);

public sealed record SoonToExpireDto(
    int BatchId,
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    DateOnly ExpiryDate,
    int DaysLeft,
    string SupplierName);

public sealed record StockItemDto(
    int ProductId,
    string Sku,
    string Name,
    string Unit,
    string? Category,
    decimal Quantity,
    decimal StockValue,
    DateOnly? NearestExpiry,
    int Batches);

public sealed record DashboardDto(
    decimal StockUnits,
    decimal StockValue,
    int ProductsInStock,
    int SoonToExpireCount,
    decimal ExpiredLoss,
    decimal Revenue30d,
    decimal GrossProfit30d,
    decimal Tax30d,
    decimal NetProfit30d,
    decimal TaxRate);
