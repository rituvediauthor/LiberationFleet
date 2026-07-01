namespace LiberationFleet.Server.Domain.Entities;

public class FallibleClickStats
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public long TotalClicks { get; set; }
    public int UniqueUserClicks { get; set; }
}
