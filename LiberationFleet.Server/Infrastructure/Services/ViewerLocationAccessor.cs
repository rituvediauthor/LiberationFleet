using LiberationFleet.Server.Application.Common.Interfaces;

namespace LiberationFleet.Server.Infrastructure.Services;

public class ViewerLocationAccessor : IViewerLocationAccessor
{
    public string? CountryCode { get; set; }
    public string? ZipCode { get; set; }
}
