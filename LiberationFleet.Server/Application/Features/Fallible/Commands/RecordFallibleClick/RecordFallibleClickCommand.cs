using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fallible.Commands.RecordFallibleClick;

public record RecordFallibleClickCommand : IRequest;

public class RecordFallibleClickCommandHandler(
    ICurrentUserService currentUser,
    IFallibleRepository fallibleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordFallibleClickCommand>
{
    public async Task Handle(RecordFallibleClickCommand request, CancellationToken cancellationToken)
    {
        await fallibleRepository.RecordClickAsync(currentUser.UserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
