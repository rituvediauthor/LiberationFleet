using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Profile.Contracts;

public class ContentPreferencesDto
{
    public AdultContentPreference AdultContentPreference { get; set; } = AdultContentPreference.Block;
}

public class ContentPreferencesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ContentPreferencesDto Preferences { get; set; } = new();
}

public class UpdateContentPreferencesRequest
{
    public AdultContentPreference AdultContentPreference { get; set; } = AdultContentPreference.Block;
}
