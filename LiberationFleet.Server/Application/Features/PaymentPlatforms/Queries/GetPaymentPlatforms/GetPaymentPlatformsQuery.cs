using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.PaymentPlatforms.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.PaymentPlatforms.Queries.GetPaymentPlatforms;

public record GetPaymentPlatformsQuery : IRequest<IReadOnlyList<PaymentPlatformDto>>;

public class GetPaymentPlatformsQueryHandler(IPaymentPlatformRepository repository)
    : IRequestHandler<GetPaymentPlatformsQuery, IReadOnlyList<PaymentPlatformDto>>
{
    public async Task<IReadOnlyList<PaymentPlatformDto>> Handle(
        GetPaymentPlatformsQuery request,
        CancellationToken cancellationToken)
    {
        var platforms = await repository.GetAllOrderedAsync(cancellationToken);
        return platforms
            .Select(p => new PaymentPlatformDto { Id = p.Id, Name = p.Name })
            .ToList();
    }
}
