using LiberationFleet.Server.Application.Features.Proposals;

namespace LiberationFleet.Server.Tests.Application.Features.Proposals;

public class ProposalNicknameGeneratorTests
{
    [Fact]
    public void Generate_DoesNotReturnATakenGuestFallback()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1000; i < 99999; i++)
        {
            taken.Add($"Guest{i}");
        }

        foreach (var adjective in new[]
                 {
                     "Gummy", "Swift", "Clever", "Bold", "Quiet", "Lucky", "Misty", "Sunny",
                     "Cosmic", "Rusty", "Silver", "Golden", "Hidden", "Witty", "Brave", "Calm",
                     "Fuzzy", "Happy", "Icy", "Jolly", "Kind", "Lunar", "Mighty", "Nimble"
                 })
        {
            foreach (var noun in new[]
                     {
                         "Bear", "Leeper", "Fox", "Otter", "Hawk", "Wolf", "Lynx", "Panda",
                         "Raven", "Tiger", "Viper", "Whale", "Badger", "Crane", "Drake", "Eagle",
                         "Finch", "Heron", "Koala", "Mantis", "Newt", "Owl", "Puma", "Quail"
                     })
            {
                taken.Add($"{adjective}{noun}");
            }
        }

        var nickname = ProposalNicknameGenerator.Generate(taken);

        nickname.Should().StartWith("Guest");
        taken.Should().NotContain(nickname);
        nickname.Length.Should().BeLessThanOrEqualTo(64);
    }

    [Fact]
    public void Generate_ReturnsUnusedAdjNounWhenAvailable()
    {
        var nickname = ProposalNicknameGenerator.Generate(["GummyBear"]);
        nickname.Should().NotBe("GummyBear");
        nickname.Should().NotBeNullOrWhiteSpace();
    }
}
