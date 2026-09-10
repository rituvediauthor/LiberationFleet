using FluentValidation;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateLocation;

public class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationCommandValidator()
    {
        When(x => x.EncryptedLocation is not null && !x.ClearLocation, () =>
        {
            RuleFor(x => x.EncryptedLocation!.Nonce)
                .NotEmpty().WithMessage("Location encryption nonce is required")
                .MaximumLength(64);
            RuleFor(x => x.EncryptedLocation!.Ciphertext)
                .NotEmpty().WithMessage("Location ciphertext is required")
                .MaximumLength(2048);
            RuleFor(x => x.EncryptedLocation!.KeyVersion)
                .GreaterThan(0).WithMessage("Location key version is required");
        });
    }
}
