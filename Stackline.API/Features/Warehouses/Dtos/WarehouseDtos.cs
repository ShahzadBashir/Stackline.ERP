namespace Stackline.API.Features.Warehouses.Dtos;

public record CreateWarehouseRequest(string Name, string? Location);
public record UpdateWarehouseRequest(string Name, string? Location, bool IsActive);
public record WarehouseResponse(Guid Id, string Name, string? Location, bool IsActive, DateTime CreatedAt);