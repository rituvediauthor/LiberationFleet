using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class FallibleRepository(ApplicationDbContext context) : IFallibleRepository
{
    public async Task RecordClickAsync(int? userId, CancellationToken cancellationToken = default)
    {
        var stats = await context.FallibleClickStats
            .SingleAsync(s => s.Id == FallibleClickStats.SingletonId, cancellationToken);

        stats.TotalClicks++;

        if (userId.HasValue)
        {
            var alreadyClicked = await context.FallibleClickUsers
                .AnyAsync(u => u.UserId == userId.Value, cancellationToken);

            if (!alreadyClicked)
            {
                context.FallibleClickUsers.Add(new FallibleClickUser
                {
                    UserId = userId.Value,
                    FirstClickedAt = DateTime.UtcNow
                });
                stats.UniqueUserClicks++;
            }
        }
    }
}
