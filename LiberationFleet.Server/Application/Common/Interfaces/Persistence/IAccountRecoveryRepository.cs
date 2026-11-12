namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

/// <summary>
/// Bulk ownership / crypto remapping for absorbing a new account into an old one.
/// Caller: <c>newUserId</c> is the logged-in recovery account; <c>oldUserId</c> survives.
/// </summary>
public interface IAccountRecoveryRepository
{
    Task TransferEncryptionIdentityAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task DropUndecryptablePersonalContentAsync(int oldUserId, CancellationToken cancellationToken = default);

    Task ReassignAuthoredContentAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task ReassignGiftRolesAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task MergeDirectMessagesAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task MergeFriendshipsAndBlocksAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task ReassignLibraryOwnershipAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task ReassignNotificationsAndPrefsAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);

    Task ReassignMiscOwnershipAsync(int newUserId, int oldUserId, CancellationToken cancellationToken = default);
}
