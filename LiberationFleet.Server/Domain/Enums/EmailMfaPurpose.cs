namespace LiberationFleet.Server.Domain.Enums;

public enum EmailMfaPurpose
{
    Login = 0,
    Enable = 1,
    Disable = 2,
    /// <summary>Verify ownership of an old account before absorbing a new account into it.</summary>
    AccountRecovery = 3
}
