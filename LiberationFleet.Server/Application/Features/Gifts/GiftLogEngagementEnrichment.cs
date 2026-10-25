using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Gifts;

internal static class GiftLogEngagementEnrichment
{
    public static async Task<T> LoadWithSchemaRetryAsync<T>(
        Func<Task<T>> load,
        Func<CancellationToken, Task> repairSchema,
        T fallback,
        ILogger logger,
        string warning,
        CancellationToken cancellationToken)
    {
        try
        {
            return await load();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogWarning(ex, warning);
            try
            {
                await repairSchema(cancellationToken);
                return await load();
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogWarning(retryEx, warning + " Retry after schema repair also failed.");
                return fallback;
            }
        }
    }
}
