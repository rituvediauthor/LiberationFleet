using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public class LibraryListPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public bool HasMore { get; init; }
}

public sealed class LibraryUnitListPage : LibraryListPage<LibraryUnit>;

public sealed class LibraryOfferingListPage : LibraryListPage<LibraryOffering>;
