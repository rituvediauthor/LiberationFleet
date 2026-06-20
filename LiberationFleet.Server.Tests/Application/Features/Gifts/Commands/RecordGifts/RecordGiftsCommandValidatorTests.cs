using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGifts;

public class RecordGiftsCommandValidatorTests
{
    private readonly RecordGiftsCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenGiftListValid_ShouldNotHaveErrors()
    {
        _validator.TestValidate(new RecordGiftsCommand(
        [
            new GiftRecordItem(25, 1, 2, null, false, "cycle")
        ])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenGiftListEmpty_ShouldHaveError()
    {
        _validator.TestValidate(new RecordGiftsCommand([]))
            .ShouldHaveValidationErrorFor(x => x.Gifts);
    }

    [Fact]
    public void Validate_WhenAmountNotPositive_ShouldHaveError()
    {
        _validator.TestValidate(new RecordGiftsCommand(
        [
            new GiftRecordItem(0, 1, 2, null, false, "cycle")
        ])).ShouldHaveValidationErrorFor("Gifts[0].Amount");
    }

    [Fact]
    public void Validate_WhenPaymentPlatformMissing_ShouldHaveError()
    {
        _validator.TestValidate(new RecordGiftsCommand(
        [
            new GiftRecordItem(10, 0, 2, null, false, "cycle")
        ])).ShouldHaveValidationErrorFor("Gifts[0].PaymentPlatformId");
    }

    [Fact]
    public void Validate_WhenRecipientMissing_ShouldHaveError()
    {
        _validator.TestValidate(new RecordGiftsCommand(
        [
            new GiftRecordItem(10, 1, 0, null, false, "cycle")
        ])).ShouldHaveValidationErrorFor("Gifts[0].RecipientId");
    }
}
