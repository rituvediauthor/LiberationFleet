using FluentValidation;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;

public class RecordGiftCommandValidator : AbstractValidator<RecordGiftCommand>
{
    public RecordGiftCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentPlatformId).GreaterThan(0);

        RuleFor(x => x)
            .Must(x => x.CompletingGiftId.HasValue ^ x.RecipientId.HasValue)
            .WithMessage("Either a recipient or a pending gift to complete must be specified.");

        When(x => x.RecipientId.HasValue, () =>
        {
            RuleFor(x => x.RecipientId!.Value).GreaterThan(0);
        });

        When(x => x.CompletingGiftId.HasValue, () =>
        {
            RuleFor(x => x.CompletingGiftId!.Value).GreaterThan(0);
        });
    }
}
