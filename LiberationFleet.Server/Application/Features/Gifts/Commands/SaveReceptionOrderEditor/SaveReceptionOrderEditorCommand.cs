using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.SaveReceptionOrderEditor;

public record SaveReceptionOrderEditorCommand(SaveReceptionOrderEditorRequest Body)
    : IRequest<SaveReceptionOrderEditorResponse>;

public class SaveReceptionOrderEditorCommandHandler(
    ICurrentUserService currentUser,
    IMutualAidService mutualAidService) : IRequestHandler<SaveReceptionOrderEditorCommand, SaveReceptionOrderEditorResponse>
{
    public async Task<SaveReceptionOrderEditorResponse> Handle(
        SaveReceptionOrderEditorCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SaveReceptionOrderEditorResponse
            {
                Success = false,
                Message = "Unauthorized."
            };
        }

        return await mutualAidService.SaveReceptionOrderEditorAsync(
            currentUser.UserId.Value,
            request.Body,
            cancellationToken);
    }
}
