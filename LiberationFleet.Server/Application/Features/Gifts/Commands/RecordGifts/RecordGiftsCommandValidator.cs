using FluentValidation;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;

public class RecordGiftsCommandValidator : AbstractValidator<RecordGiftsCommand>
{
    public RecordGiftsCommandValidator()
    {
        RuleFor(x => x.Gifts).NotEmpty();
        RuleForEach(x => x.Gifts).ChildRules(gift =>
        {
            gift.RuleFor(x => x.Amount).GreaterThan(0);
            gift.RuleFor(x => x.PaymentPlatformId).GreaterThan(0);
            gift.RuleFor(x => x.RecipientId).GreaterThan(0);
        });
    }
}
