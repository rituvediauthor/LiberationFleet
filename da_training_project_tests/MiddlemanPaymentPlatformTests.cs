using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;
using LiberationFleet.Server.Domain.Entities;

namespace da_training_project_tests;

public class MiddlemanPaymentPlatformTests
{
    [Fact]
    public async Task MiddlemanDetection_SharesPlatformWithBoth_IsValidMiddleman()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Recipient");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Middleman");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 1, Handle = "mid@paypal" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 2, Handle = "mid@cashapp" });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 20m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var entry = response.Recipients.Single(r => r.UserId == recipient.Id);
        entry.SuggestedMiddlemanId.Should().Be(middleman.Id);
        entry.PaymentNote.Should().Contain("Middleman");
    }

    [Fact]
    public async Task DirectGiftPossible_WhenUsersSharePaymentPlatform()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "DirectRecip");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 1, Handle = "recipient@paypal" });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 20m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var entry = response.Recipients.Single(r => r.UserId == recipient.Id);
        entry.CommonPaymentPlatforms.Should().Contain(1);
        entry.PaymentNote.Should().Be("Direct payment available");
    }

    [Fact]
    public async Task NoSharedPlatform_NoMiddleman_ShowsNoSuitableMiddlemanNote()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "IsolatedRecip");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 20m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateGetReceptionOrderHandler(context, giver.Id);
        var response = await handler.Handle(new GetReceptionOrderQuery(30), CancellationToken.None);

        var entry = response.Recipients.Single(r => r.UserId == recipient.Id);
        entry.SuggestedMiddlemanId.Should().BeNull();
        entry.PaymentNote.Should().Be("No suitable middleman");
    }

    [Fact]
    public async Task MultipleMiddlemen_NoDefaultSelected_UserMustChoose()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "MultiRecip");
        var mm1 = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "MM1");
        var mm2 = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "MM2");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "recipient@cashapp" },
            new UserPaymentPlatform { UserId = mm1.Id, PaymentPlatformId = 1, Handle = "mm1@paypal" },
            new UserPaymentPlatform { UserId = mm1.Id, PaymentPlatformId = 2, Handle = "mm1@cashapp" },
            new UserPaymentPlatform { UserId = mm2.Id, PaymentPlatformId = 1, Handle = "mm2@paypal" },
            new UserPaymentPlatform { UserId = mm2.Id, PaymentPlatformId = 2, Handle = "mm2@cashapp" });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 20m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var receptionService = MutualAidTestFixture.CreateReceptionOrderService(context);
        var order = await receptionService.GetOrderedRecipientsAsync(crew.Id, giver.Id);

        var entry = order.Single(r => r.UserId == recipient.Id);
        entry.SuggestedMiddlemanId.Should().BeNull();
    }
}
