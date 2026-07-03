using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.TestHelpers;

public sealed class MutualAidSeasonFixture : IAsyncDisposable
{
    public MutualAidSeasonFixture(
        ApplicationDbContext context,
        MutualAidService service,
        Crew crew,
        User alice,
        User bob,
        User carol,
        IReadOnlyDictionary<string, CrewPaymentPlatform> platforms,
        DateTime seasonStart)
    {
        Context = context;
        Service = service;
        Crew = crew;
        Alice = alice;
        Bob = bob;
        Carol = carol;
        Platforms = platforms;
        SeasonStart = seasonStart;
    }

    public ApplicationDbContext Context { get; }
    public MutualAidService Service { get; }
    public Crew Crew { get; }
    public User Alice { get; }
    public User Bob { get; }
    public User Carol { get; }
    public IReadOnlyDictionary<string, CrewPaymentPlatform> Platforms { get; }
    public DateTime SeasonStart { get; }

    public static async Task<MutualAidSeasonFixture> CreateActiveSeasonAsync(
        decimal monthlyContribution = 100m,
        decimal cycleCap = 600m)
    {
        var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedPaymentPlatformsAsync(context);

        var alice = CreateUser("alice", "alice@example.com");
        var bob = CreateUser("bob", "bob@example.com");
        var carol = CreateUser("carol", "carol@example.com");
        context.Users.AddRange(alice, bob, carol);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Mutual Aid Test Crew",
            MaxSize = 10,
            Privacy = Domain.Enums.CrewPrivacy.Public,
            Scope = Domain.Enums.CrewScope.Online,
            JoinCode = "AIDCREW1",
            CreatedByUserId = alice.Id,
            CreatedAt = DateTime.UtcNow,
            SeasonStarted = true,
            CurrentSeasonStartDate = DateTime.UtcNow.AddDays(-10),
            SeasonMemberCycleCap = cycleCap,
            SeasonNonMemberCycleCap = cycleCap
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var platforms = await TestDbContextFactory.SeedCrewPaymentPlatformsAsync(context, crew.Id);
        var seasonStart = crew.CurrentSeasonStartDate!.Value;

        context.CrewMemberships.AddRange(
            CreateMembership(alice, crew, monthlyContribution),
            CreateMembership(bob, crew, monthlyContribution),
            CreateMembership(carol, crew, monthlyContribution));
        await context.SaveChangesAsync();

        context.UserPaymentPlatforms.AddRange(
            CreatePlatformAccount(alice, platforms["PayPal"], "@alice-paypal", isPreferred: true),
            CreatePlatformAccount(bob, platforms["Venmo"], "@bob-venmo", isPreferred: true),
            CreatePlatformAccount(carol, platforms["PayPal"], "@carol-paypal"),
            CreatePlatformAccount(carol, platforms["Venmo"], "@carol-venmo", isPreferred: true));
        await context.SaveChangesAsync();

        context.SeasonCycles.AddRange(
            CreateCycle(crew, bob, seasonStart, cycleCap, receptionOrderPosition: 0, priorityScore: 300m),
            CreateCycle(crew, alice, seasonStart, cycleCap, receptionOrderPosition: 1, priorityScore: 200m),
            CreateCycle(crew, carol, seasonStart, cycleCap, receptionOrderPosition: 2, priorityScore: 100m));
        await context.SaveChangesAsync();

        var service = HandlerTestFixture.CreateMutualAidService(context);

        return new MutualAidSeasonFixture(context, service, crew, alice, bob, carol, platforms, seasonStart);
    }

    public async Task<MonthlySurvivalThreshold> AddUnsatisfiedThresholdAsync(
        User recipient,
        decimal thresholdAmount,
        decimal receivedAmount = 0m,
        int receptionOrderPosition = 0)
    {
        var threshold = new MonthlySurvivalThreshold
        {
            CrewId = Crew.Id,
            UserId = recipient.Id,
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            ThresholdAmount = thresholdAmount,
            ReceivedAmount = receivedAmount,
            ReceptionOrderPosition = receptionOrderPosition,
            Satisfied = false
        };

        Context.MonthlySurvivalThresholds.Add(threshold);
        await Context.SaveChangesAsync();
        return threshold;
    }

    public async Task SetInSeasonAsync(User user, bool isInSeason)
    {
        var membership = await Context.CrewMemberships.SingleAsync(m => m.UserId == user.Id && m.CrewId == Crew.Id);
        membership.IsInSeason = isInSeason;
        await Context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();

    private static User CreateUser(string username, string email) =>
        new()
        {
            Username = username,
            Email = email,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            InNeedOfAid = true
        };

    private static CrewMembership CreateMembership(User user, Crew crew, decimal monthlyContribution) =>
        new()
        {
            UserId = user.Id,
            CrewId = crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow,
            EstimatedMonthlyContribution = monthlyContribution,
            IsSeasonReady = true,
            IsInSeason = true,
            IsHonoraryMember = true,
            CurrentPriorityScore = user.Username switch
            {
                "bob" => 300m,
                "alice" => 200m,
                "carol" => 100m,
                _ => 0m
            }
        };

    private static UserPaymentPlatform CreatePlatformAccount(
        User user,
        CrewPaymentPlatform platform,
        string handle,
        bool isPreferred = false) =>
        new()
        {
            UserId = user.Id,
            CrewPaymentPlatformId = platform.Id,
            CrewPaymentPlatform = platform,
            Handle = handle,
            IsPreferred = isPreferred
        };

    private static SeasonCycle CreateCycle(
        Crew crew,
        User user,
        DateTime seasonStart,
        decimal cycleCap,
        int receptionOrderPosition,
        decimal priorityScore) =>
        new()
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = seasonStart,
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = priorityScore,
            ReceptionOrderPosition = receptionOrderPosition,
            HasCycleStarted = false
        };
}
