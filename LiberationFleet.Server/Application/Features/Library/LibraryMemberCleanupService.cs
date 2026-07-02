using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public class LibraryMemberCleanupService(
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    LibraryRequestCleanupHelper requestCleanupHelper)
{
    public async Task CleanupForDepartingMemberAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var requesterRequests = await libraryRepository.GetTrackedRequestsByRequesterAsync(
            crewId,
            userId,
            cancellationToken);
        foreach (var request in requesterRequests)
        {
            await requestCleanupHelper.CancelRequestWithMessagesAsync(request.Id, cancellationToken);
            request.Status = LibraryRequestStatus.Cancelled;
            request.UpdatedAt = DateTime.UtcNow;
        }

        var possessedUnits = await libraryRepository.GetTrackedUnitsPossessedByUserAsync(
            crewId,
            userId,
            cancellationToken);
        foreach (var unit in possessedUnits)
        {
            await requestCleanupHelper.CancelActiveRequestsForUnitAsync(unit.Id, cancellationToken);
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.LibraryBrokenReport,
                [unit.Id.ToString()],
                cancellationToken);
        }

        await libraryRepository.CleanupMemberLibraryDataAsync(crewId, userId, cancellationToken);
    }
}
