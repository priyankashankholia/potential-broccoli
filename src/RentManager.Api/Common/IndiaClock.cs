namespace RentManager.Api.Common;

// All dates in this app are India calendar dates.
// Earlier the code was mixing DateTime.UtcNow, DateTime.Now and
// UTC-midnight values, which is why "27 Aug, due 30 Aug" came out wrong.
// Now everything goes through here and dates are stored as DateOnly.
public static class IndiaClock
{
    private static readonly TimeZoneInfo IndiaTimeZone = ResolveIndiaTimeZone();

    public static DateOnly Today()
    {
        var indiaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

        return DateOnly.FromDateTime(indiaNow);
    }

    // Only for audit stamps where we actually want a time.
    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);
    }

    // Due day 31 in February has to become 28 (or 29), so clamp it.
    public static DateOnly DueDateFor(int year, int month, int preferredDay)
    {
        var day = Math.Clamp(preferredDay, 1, DateTime.DaysInMonth(year, month));

        return new DateOnly(year, month, day);
    }

    // First month whose due date hasn't already gone past. This is what
    // "upcoming applicable month" means when adding a tenant.
    //
    // Adding on 27 Aug:
    //   due day 1  -> September, because 1 Aug is already behind us
    //   due day 30 -> August, because 30 Aug is still ahead
    //
    // Without this, picking "this month" on the 27th would create an
    // August rent that was overdue the second it got created.
    public static (int Year, int Month) FirstApplicableMonth(DateOnly today, int dueDay)
    {
        var thisMonthDue = DueDateFor(today.Year, today.Month, dueDay);

        if (thisMonthDue >= today)
        {
            return (today.Year, today.Month);
        }

        var next = today.AddMonths(1);

        return (next.Year, next.Month);
    }

    public static (int Year, int Month) AddMonths(int year, int month, int monthsToAdd)
    {
        return MonthFromKey(MonthKey(year, month) + monthsToAdd);
    }

    // Positive means the target is in the future. 27 Aug -> 30 Aug gives 3.
    public static int DaysBetween(DateOnly from, DateOnly to)
    {
        return to.DayNumber - from.DayNumber;
    }

    // Turns a year/month into one sortable number so we can compare and
    // loop over months without messing about with dates.
    public static int MonthKey(int year, int month) => (year * 12) + (month - 1);

    public static (int Year, int Month) MonthFromKey(int key)
        => (key / 12, (key % 12) + 1);

    public static string MonthLabel(int year, int month)
        => new DateTime(year, month, 1).ToString("MMMM yyyy");

    private static TimeZoneInfo ResolveIndiaTimeZone()
    {
        // Windows and Linux use different ids for the same timezone.
        foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Fallback. IST is a fixed +05:30 with no DST, so this is safe.
        return TimeZoneInfo.CreateCustomTimeZone(
            "IST",
            TimeSpan.FromMinutes(330),
            "India Standard Time",
            "India Standard Time");
    }
}
