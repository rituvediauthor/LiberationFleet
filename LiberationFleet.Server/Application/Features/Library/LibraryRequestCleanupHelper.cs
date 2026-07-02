using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public class LibraryRequestCleanupHelper(
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository)
{
    public async Task CancelRequestWithMessagesAsync(
        int requestId,
        CancellationToken cancellationToken)
    {
        var messageIds = await libraryRepository.DeleteMessagesForRequestAsync(requestId, cancellationToken);
        if (messageIds.Count > 0)
        {
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.LibraryRequestMessage,
                messageIds.Select(id => id.ToString()).ToList(),
                cancellationToken);
        }

        await cryptoRepository.DeleteEnvelopesAsync(
            EncryptedContentType.LibraryRequest,
            [requestId.ToString()],
            cancellationToken);
    }

    public async Task CancelActiveRequestsForUnitAsync(
        int unitId,
        CancellationToken cancellationToken)
    {
        var requests = await libraryRepository.GetTrackedCancellableRequestsForUnitAsync(unitId, cancellationToken);
        foreach (var request in requests)
        {
            await CancelRequestWithMessagesAsync(request.Id, cancellationToken);
            request.Status = LibraryRequestStatus.Cancelled;
            request.UpdatedAt = DateTime.UtcNow;
        }
    }
}
