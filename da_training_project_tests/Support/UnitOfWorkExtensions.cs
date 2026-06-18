using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

/// <summary>
/// Test-assembly extension so linked production services can query via IUnitOfWork.
/// </summary>
public static class UnitOfWorkExtensions
{
    public static DbSet<TEntity> Set<TEntity>(this IUnitOfWork unitOfWork)
        where TEntity : class
    {
        if (unitOfWork is not ApplicationDbContext context)
        {
            throw new InvalidOperationException("IUnitOfWork must be an ApplicationDbContext for data access.");
        }

        return context.Set<TEntity>();
    }
}
