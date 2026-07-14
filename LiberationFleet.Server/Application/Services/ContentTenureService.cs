using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public class ContentTenureService(
    IContentTenureRepository tenureRepository,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository)
{
    public async Task<int> GetCrewTenureDaysAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var tenure = await tenureRepository.GetCrewTenureAsync(userId, crewId, cancellationToken);
        return ContentTenureCalculator.GetTenureDays(tenure, DateTime.UtcNow);
    }

    public async Task<int> GetFleetTenureDaysAsync(int userId, int fleetId, CancellationToken cancellationToken = default)
    {
        var tenure = await tenureRepository.GetFleetTenureAsync(userId, fleetId, cancellationToken);
        return ContentTenureCalculator.GetTenureDays(tenure, DateTime.UtcNow);
    }

    public async Task OnJoinedCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        await ResumeCrewAsync(userId, crewId, cancellationToken);

        var fleet = await fleetRepository.GetFleetForCrewAsync(crewId, cancellationToken);
        if (fleet is not null)
        {
            await ResumeFleetAsync(userId, fleet.Id, cancellationToken);
        }
    }

    public async Task OnLeftCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var fleet = await fleetRepository.GetFleetForCrewAsync(crewId, cancellationToken);
        await PauseCrewAsync(userId, crewId, cancellationToken);

        if (fleet is not null)
        {
            await PauseFleetAsync(userId, fleet.Id, cancellationToken);
        }
    }

    public async Task OnCrewJoinedFleetAsync(int crewId, int fleetId, CancellationToken cancellationToken = default)
    {
        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        foreach (var member in members)
        {
            if (member.IsPlaceholderMember || member.User.IsCrewGiftRecipient)
            {
                continue;
            }

            await ResumeFleetAsync(member.UserId, fleetId, cancellationToken);
        }
    }

    public async Task OnCrewLeftFleetAsync(int crewId, int fleetId, CancellationToken cancellationToken = default)
    {
        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        foreach (var member in members)
        {
            await PauseFleetAsync(member.UserId, fleetId, cancellationToken);
        }
    }

    public async Task ResumeCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var tenure = await tenureRepository.GetCrewTenureAsync(userId, crewId, cancellationToken);
        if (tenure is null)
        {
            await tenureRepository.AddCrewTenureAsync(new UserCrewContentTenure
            {
                UserId = userId,
                CrewId = crewId,
                AccruedTicks = 0,
                ClockStartedAtUtc = utcNow
            }, cancellationToken);
            return;
        }

        var accrued = tenure.AccruedTicks;
        var clock = tenure.ClockStartedAtUtc;
        ContentTenureCalculator.Resume(ref accrued, ref clock, utcNow);
        tenure.AccruedTicks = accrued;
        tenure.ClockStartedAtUtc = clock;
    }

    public async Task PauseCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var tenure = await tenureRepository.GetCrewTenureAsync(userId, crewId, cancellationToken);
        if (tenure is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var accrued = tenure.AccruedTicks;
        var clock = tenure.ClockStartedAtUtc;
        ContentTenureCalculator.Pause(ref accrued, ref clock, utcNow);
        tenure.AccruedTicks = accrued;
        tenure.ClockStartedAtUtc = clock;
    }

    public async Task ResumeFleetAsync(int userId, int fleetId, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var tenure = await tenureRepository.GetFleetTenureAsync(userId, fleetId, cancellationToken);
        if (tenure is null)
        {
            await tenureRepository.AddFleetTenureAsync(new UserFleetContentTenure
            {
                UserId = userId,
                FleetId = fleetId,
                AccruedTicks = 0,
                ClockStartedAtUtc = utcNow
            }, cancellationToken);
            return;
        }

        var accrued = tenure.AccruedTicks;
        var clock = tenure.ClockStartedAtUtc;
        ContentTenureCalculator.Resume(ref accrued, ref clock, utcNow);
        tenure.AccruedTicks = accrued;
        tenure.ClockStartedAtUtc = clock;
    }

    public async Task PauseFleetAsync(int userId, int fleetId, CancellationToken cancellationToken = default)
    {
        var tenure = await tenureRepository.GetFleetTenureAsync(userId, fleetId, cancellationToken);
        if (tenure is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var accrued = tenure.AccruedTicks;
        var clock = tenure.ClockStartedAtUtc;
        ContentTenureCalculator.Pause(ref accrued, ref clock, utcNow);
        tenure.AccruedTicks = accrued;
        tenure.ClockStartedAtUtc = clock;
    }
}
