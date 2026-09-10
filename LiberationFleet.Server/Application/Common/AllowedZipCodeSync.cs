using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class AllowedZipCodeSync
{
    public static void SetCrewZips(Crew crew, IEnumerable<string>? zips)
    {
        var normalized = ZipCodeList.Normalize(zips);
        crew.AllowedZipCodes.Clear();
        foreach (var zip in normalized)
        {
            crew.AllowedZipCodes.Add(new CrewAllowedZipCode { ZipCode = zip });
        }
    }

    public static void SetFleetZips(Fleet fleet, IEnumerable<string>? zips)
    {
        var normalized = ZipCodeList.Normalize(zips);
        fleet.AllowedZipCodes.Clear();
        foreach (var zip in normalized)
        {
            fleet.AllowedZipCodes.Add(new FleetAllowedZipCode { ZipCode = zip });
        }
    }

    public static void SetOfferingZips(LibraryOffering offering, IEnumerable<string>? zips)
    {
        var normalized = ZipCodeList.Normalize(zips);
        offering.AllowedZipCodes.Clear();
        foreach (var zip in normalized)
        {
            offering.AllowedZipCodes.Add(new LibraryOfferingAllowedZipCode { ZipCode = zip });
        }
    }

    public static IReadOnlyList<string> GetCrewZips(Crew crew) =>
        ZipCodeList.Normalize(crew.AllowedZipCodes.Select(z => z.ZipCode));

    public static IReadOnlyList<string> GetFleetZips(Fleet fleet) =>
        ZipCodeList.Normalize(fleet.AllowedZipCodes.Select(z => z.ZipCode));

    public static IReadOnlyList<string> GetOfferingZips(LibraryOffering offering) =>
        ZipCodeList.Normalize(offering.AllowedZipCodes.Select(z => z.ZipCode));
}
