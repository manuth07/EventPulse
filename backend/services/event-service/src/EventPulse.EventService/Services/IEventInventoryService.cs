using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

public interface IEventInventoryService
{
    Task<InventoryOperationResult> ReserveInventoryAsync(
        Guid eventId,
        ReserveInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryOperationResult> CommitInventoryAsync(
        Guid eventId,
        CommitInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryOperationResult> ReleaseInventoryAsync(
        Guid eventId,
        ReleaseInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task<InventoryOperationResult> RestoreInventoryAsync(
        Guid eventId,
        RestoreInventoryRequest request,
        CancellationToken cancellationToken = default);
}
