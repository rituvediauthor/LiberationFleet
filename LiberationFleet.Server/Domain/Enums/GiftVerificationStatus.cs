namespace LiberationFleet.Server.Domain.Enums;

public enum GiftVerificationStatus
{
    Pending = 0,
    TransferNotReceived = 1,
    MiddlemanReceivedFunds = 2,
    MiddlemanCannotComplete = 3,
    AwaitingRecipientVerification = 4,
    RecipientNotReceived = 5,
    Verified = 6
}
