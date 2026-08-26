using LiberationFleet.Server.Application.Features.Chats.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.ToggleAnonymousMode;

public record ToggleAnonymousModeCommand(int RoomId, bool Enabled) : IRequest<ChatOperationResponse>;

/// <summary>
/// Room-wide anonymous mode is deprecated. Anonymity is a personal compose preference.
/// </summary>
public class ToggleAnonymousModeCommandHandler : IRequestHandler<ToggleAnonymousModeCommand, ChatOperationResponse>
{
    public Task<ChatOperationResponse> Handle(ToggleAnonymousModeCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ChatOperationResponse
        {
            Success = false,
            Message = "Anonymous posting is a personal preference. Use the anonymous toggle while composing — it only affects your messages."
        });
    }
}
