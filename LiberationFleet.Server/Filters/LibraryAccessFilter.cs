using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LiberationFleet.Server.Filters;

public sealed class LibraryAccessFilter(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!currentUser.UserId.HasValue)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            context.HttpContext.RequestAborted);
        if (membership is null)
        {
            context.Result = new BadRequestObjectResult(new { success = false, message = "You are not in a crew." });
            return;
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, context.HttpContext.RequestAborted);
        if (crew is null)
        {
            context.Result = new BadRequestObjectResult(new { success = false, message = "Crew not found." });
            return;
        }

        var deniedMessage = LibraryAccessService.GetAccessDeniedMessage(crew, membership);
        if (deniedMessage is not null)
        {
            context.Result = new BadRequestObjectResult(new { success = false, message = deniedMessage });
            return;
        }

        await next();
    }
}
