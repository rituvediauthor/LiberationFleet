using LiberationFleet.Server.Application.Common.Interfaces;

namespace LiberationFleet.Server.Infrastructure.Geocoding;

/// <summary>
/// Development zip distance provider using an in-memory coordinate table.
/// Replace with a third-party geocoding API for production use.
/// </summary>
public class ZipCodeDistanceService : IZipCodeDistanceService
{
    private static readonly Dictionary<string, (double Lat, double Lng)> ZipCoordinates = new()
    {
        ["10001"] = (40.7506, -73.9971),
        ["10002"] = (40.7157, -73.9873),
        ["90210"] = (34.1030, -118.4105),
        ["60601"] = (41.8853, -87.6217),
        ["77001"] = (29.7604, -95.3698),
        ["85001"] = (33.4484, -112.0740),
        ["19101"] = (39.9526, -75.1652),
        ["98101"] = (47.6113, -122.3343),
        ["30301"] = (33.7490, -84.3880),
        ["02108"] = (42.3588, -71.0707),
        ["33101"] = (25.7791, -80.1978),
        ["80202"] = (39.7508, -104.9966),
        ["94102"] = (37.7793, -122.4193),
        ["75201"] = (32.7875, -96.7970),
        ["20001"] = (38.9072, -77.0369)
    };

    public bool TryGetDistanceMiles(string fromZipCode, string toZipCode, out double distanceMiles)
    {
        distanceMiles = 0;
        var from = NormalizeZip(fromZipCode);
        var to = NormalizeZip(toZipCode);

        if (!ZipCoordinates.TryGetValue(from, out var fromCoord) ||
            !ZipCoordinates.TryGetValue(to, out var toCoord))
        {
            return false;
        }

        distanceMiles = HaversineMiles(fromCoord.Lat, fromCoord.Lng, toCoord.Lat, toCoord.Lng);
        return true;
    }

    private static string NormalizeZip(string zipCode) =>
        zipCode.Trim().PadLeft(5, '0')[..5];

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMiles = 3958.8;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMiles * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
