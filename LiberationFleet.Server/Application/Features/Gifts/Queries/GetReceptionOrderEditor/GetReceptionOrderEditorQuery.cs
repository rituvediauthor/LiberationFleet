using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrderEditor;

public record GetReceptionOrderEditorQuery : IRequest<ReceptionOrderEditorResponse>;

public class GetReceptionOrderEditorQueryHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService) : IRequestHandler<GetReceptionOrderEditorQuery, ReceptionOrderEditorResponse>
{
    public async Task<ReceptionOrderEditorResponse> Handle(
        GetReceptionOrderEditorQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ReceptionOrderEditorResponse
            {
                Success = false,
                Message = "Unauthorized."
            };
        }

        return await mutualAidService.GetReceptionOrderEditorAsync(currentUser.UserId.Value, cancellationToken);
    }
}
