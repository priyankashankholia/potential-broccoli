using RentManager.Api.Common;
using RentManager.Api.Models;

namespace RentManager.Api.Services;

// Single place that decides a rent's status. The API sends the result to
// Angular so the browser's clock can never disagree with the server.
//
//   fully paid          -> Paid (sticky, never goes back)
//   today  < due date   -> Upcoming
//   today == due date   -> Pending
//   today  > due date   -> Outstanding
//
// A partial payment does not change the status, it only reduces the
// balance. Whatever is left stays Outstanding once the due date passes.
public static class RentStatusCalculator
{
    // A rent only becomes collectable 3 days before its due date. Before
    // that it shows on the card as Upcoming but is not part of the
    // tenant's payable total.
    public const int PayableWindowDays = 3;

    public static RentStatusInfo For(Rent rent, DateOnly today)
    {
        var remaining = Math.Max(0m, rent.AmountDue - rent.AmountPaid);
        var daysUntilDue = IndiaClock.DaysBetween(today, rent.DueDate);

        if (remaining <= 0m)
        {
            return new RentStatusInfo(
                Status: "Paid",
                Remaining: 0m,
                DaysUntilDue: daysUntilDue,
                IsDueSoon: false,
                IsPayable: false,
                Timing: "Paid this month");
        }

        var status =
            daysUntilDue > 0 ? "Upcoming" :
            daysUntilDue == 0 ? "Pending" :
            "Outstanding";

        var timing = daysUntilDue switch
        {
            > 1 => $"Due in {daysUntilDue} days",
            1 => "Due tomorrow",
            0 => "Due today",
            -1 => "1 day overdue",
            _ => $"{Math.Abs(daysUntilDue)} days overdue"
        };

        return new RentStatusInfo(
            Status: status,
            Remaining: remaining,
            DaysUntilDue: daysUntilDue,
            IsDueSoon: daysUntilDue > 0 && daysUntilDue <= PayableWindowDays,
            IsPayable: IsPayable(rent, today),
            Timing: timing);
    }

    // Inside the 3-day window, on the due date, or overdue. A month that
    // already has a part payment stays payable so the balance can be
    // completed at any time.
    public static bool IsPayable(Rent rent, DateOnly today)
    {
        if (rent.AmountPaid >= rent.AmountDue)
        {
            return false;
        }

        if (rent.AmountPaid > 0m)
        {
            return true;
        }

        return IndiaClock.DaysBetween(today, rent.DueDate) <= PayableWindowDays;
    }
}

public record RentStatusInfo(
    string Status,
    decimal Remaining,
    int DaysUntilDue,
    bool IsDueSoon,
    bool IsPayable,
    string Timing);
