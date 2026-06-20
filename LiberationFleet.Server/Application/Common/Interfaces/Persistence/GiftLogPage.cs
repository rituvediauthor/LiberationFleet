using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public sealed class GiftLogPage
{
    public IReadOnlyList<Gift> Items { get; init; } = Array.Empty<Gift>();
    public bool HasMore { get; init; }
}
