using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IPaymentPlatformRepository
{
    Task<IReadOnlyList<PaymentPlatform>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
