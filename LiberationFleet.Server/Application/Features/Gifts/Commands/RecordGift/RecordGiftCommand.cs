using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;

public record RecordGiftCommand(
    decimal Amount,
    int PaymentPlatformId,
    int? RecipientId,
    int? MiddlemanId,
    int? CompletingGiftId) : IRequest<GiftOperationResponse>;
