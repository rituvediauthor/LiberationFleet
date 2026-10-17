using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Features.Proposals;

/// <summary>
/// GET handlers persist timer/apply/alias side effects, but a failed save must not 500 the read.
/// </summary>
internal static class ProposalReadPersistence
{
    public static async Task TrySaveAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Mapping still returns. Timer/apply/alias persist on the next successful write.
        }
    }
}
