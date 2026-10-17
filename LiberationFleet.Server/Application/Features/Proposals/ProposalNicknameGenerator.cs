namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalNicknameGenerator
{
    private static readonly string[] Adjectives =
    [
        "Gummy", "Swift", "Clever", "Bold", "Quiet", "Lucky", "Misty", "Sunny",
        "Cosmic", "Rusty", "Silver", "Golden", "Hidden", "Witty", "Brave", "Calm",
        "Fuzzy", "Happy", "Icy", "Jolly", "Kind", "Lunar", "Mighty", "Nimble"
    ];

    private static readonly string[] Nouns =
    [
        "Bear", "Leeper", "Fox", "Otter", "Hawk", "Wolf", "Lynx", "Panda",
        "Raven", "Tiger", "Viper", "Whale", "Badger", "Crane", "Drake", "Eagle",
        "Finch", "Heron", "Koala", "Mantis", "Newt", "Owl", "Puma", "Quail"
    ];

    public static string Generate(IReadOnlyCollection<string> takenNicknames)
    {
        var taken = new HashSet<string>(takenNicknames, StringComparer.OrdinalIgnoreCase);

        for (var attempt = 0; attempt < 400; attempt++)
        {
            var nickname = $"{Pick(Adjectives)}{Pick(Nouns)}";
            if (taken.Add(nickname))
            {
                return nickname;
            }
        }

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var guest = $"Guest{Random.Shared.Next(1000, 99999)}";
            if (taken.Add(guest))
            {
                return guest;
            }
        }

        return $"Guest{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static string Pick(IReadOnlyList<string> values) =>
        values[Random.Shared.Next(values.Count)];
}
