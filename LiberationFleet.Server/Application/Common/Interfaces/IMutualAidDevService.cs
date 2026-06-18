namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IMutualAidDevService
{
    Task<DevActionResultDto> SimulateNewMonthAsync(int userId, CancellationToken cancellationToken = default);
    Task<DevActionResultDto> SimulateNewSeasonAsync(int userId, CancellationToken cancellationToken = default);
    Task<DevActionResultDto> CompleteAllCyclesAsync(int userId, CancellationToken cancellationToken = default);
    Task<DevActionResultDto> ResetSeasonAsync(int userId, CancellationToken cancellationToken = default);
    Task<DevActionResultDto> RecalculateCapsAsync(int userId, CancellationToken cancellationToken = default);
}

public class DevActionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
