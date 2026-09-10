namespace LiberationFleet.Server.Application.Common.Interfaces;

/// <summary>
/// Ephemeral viewer country/postal from the current request (headers or search body).
/// Not loaded from stored user profile — profile location is client-encrypted.
/// </summary>
public interface IViewerLocationAccessor
{
    string? CountryCode { get; }
    string? ZipCode { get; }
}
