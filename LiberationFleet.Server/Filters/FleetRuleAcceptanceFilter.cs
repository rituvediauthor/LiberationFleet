using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LiberationFleet.Server.Filters;

/// <summary>
/// Blocks fleet-scoped API actions until the user has accepted current public fleet rules.
/// Controllers/actions that must remain reachable should opt out with
/// <see cref="SkipFleetRuleAcceptanceAttribute"/>.
/// </summary>
public sealed class FleetRuleAcceptanceFilter(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IUserFleetRuleAcceptanceRepository acceptanceRepository) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<SkipFleetRuleAcceptanceAttribute>().Any())
        {
            await next();
            return;
        }

        if (!currentUser.UserId.HasValue)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userId = currentUser.UserId.Value;
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, context.HttpContext.RequestAborted);
        if (fleet is null)
        {
            await next();
            return;
        }

        if (await FleetRuleAcceptanceHelper.NeedsRuleAcceptanceAsync(
                userId,
                fleet,
                fleetRepository,
                acceptanceRepository,
                context.HttpContext.RequestAborted))
        {
            context.Result = new BadRequestObjectResult(new
            {
                success = false,
                message = "You must accept the current fleet rules before continuing.",
                needsRuleAcceptance = true
            });
            return;
        }

        await next();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipFleetRuleAcceptanceAttribute : Attribute;
