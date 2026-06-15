namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IZipCodeDistanceService
{
    bool TryGetDistanceMiles(string fromZipCode, string toZipCode, out double distanceMiles);
}
