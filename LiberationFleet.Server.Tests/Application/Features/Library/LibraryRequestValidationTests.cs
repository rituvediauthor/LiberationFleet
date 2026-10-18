using LiberationFleet.Server.Application.Features.Library;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryRequestValidationTests
{
    [Fact]
    public void NormalizeDateRange_WithoutTimeZone_KeepsUtcMidnightDates()
    {
        var start = new DateTime(2026, 8, 18, 16, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 18, 16, 0, 0, DateTimeKind.Utc);

        var (normalizedStart, normalizedEnd) = LibraryRequestValidation.NormalizeDateRange(start, end);

        normalizedStart.Should().Be(new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));
        normalizedEnd.Should().Be(new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));
        LibraryRequestLifecycle.GetExclusiveExpiryUtc(normalizedEnd)
            .Should().Be(new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NormalizeDateRange_EasternTimeZone_ExpiresAtLocalEndOfDueDate()
    {
        var dueDate = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);

        var (_, normalizedEnd) = LibraryRequestValidation.NormalizeDateRange(
            dueDate,
            dueDate,
            "America/New_York");

        // August is EDT (UTC-4); local midnight Aug 19 = 04:00 UTC.
        // Offset is stored on NeededByEnd so Date stays Aug 18 for display.
        normalizedEnd.Should().Be(new DateTime(2026, 8, 18, 4, 0, 0, DateTimeKind.Utc));
        LibraryRequestLifecycle.GetExclusiveExpiryUtc(normalizedEnd)
            .Should().Be(new DateTime(2026, 8, 19, 4, 0, 0, DateTimeKind.Utc));
        normalizedEnd.ToString("yyyy-MM-dd").Should().Be("2026-08-18");
    }

    [Fact]
    public void NormalizeDateRange_UnknownTimeZone_KeepsUtcMidnightDates()
    {
        var dueDate = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);

        var (_, normalizedEnd) = LibraryRequestValidation.NormalizeDateRange(
            dueDate,
            dueDate,
            "Not/AZone");

        normalizedEnd.Should().Be(dueDate);
    }

    [Fact]
    public void GetLocalCalendarDate_EasternEvening_IsPreviousUtcDate()
    {
        var utcNow = new DateTime(2026, 8, 19, 2, 30, 0, DateTimeKind.Utc);

        LibraryRequestValidation.GetLocalCalendarDate(utcNow, "America/New_York")
            .Should().Be(new DateTime(2026, 8, 18));
        LibraryRequestValidation.GetLocalCalendarDate(utcNow, null)
            .Should().Be(new DateTime(2026, 8, 19));
    }
}
