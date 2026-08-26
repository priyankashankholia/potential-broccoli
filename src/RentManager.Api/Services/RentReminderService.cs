using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Services;

public class RentReminderService
{
    private readonly RentManagerDbContext _db;

    public RentReminderService(RentManagerDbContext db)
    {
        _db = db;
    }

    public async Task<int> GenerateRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var rents = await _db.Rents
            .Include(r => r.Tenant)
            .Where(r =>
                !r.IsSettled &&
                r.Tenant != null)
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var rent in rents)
        {
            var daysFromDueDate =
                (rent.DueDate.Date - today).Days;

            string? reminderType = daysFromDueDate switch
            {
                3 => "RentDue3Days",
                2 => "RentDue2Days",
                1 => "RentDue1Day",
                0 => "RentDueToday",
                -1 => "RentOverdue1Day",
                -2 => "RentOverdue2Days",
                -7 => "RentOverdue7Days",
                _ => null
            };

            if (reminderType is null)
            {
                continue;
            }

            var alreadyCreated = await _db.Notifications
                .AnyAsync(
                    n =>
                        n.RentId == rent.Id &&
                        n.Type == reminderType,
                    cancellationToken);

            if (alreadyCreated)
            {
                continue;
            }

            var remaining =
                rent.AmountDue - rent.AmountPaid;

            string message;

            if (daysFromDueDate > 0)
            {
                message =
                    $"Rent reminder: ₹{remaining} is due on {rent.DueDate:dd MMM yyyy}. Please make the payment by the due date.";
            }
            else if (daysFromDueDate == 0)
            {
                message =
                    $"Rent reminder: ₹{remaining} is due today.";
            }
            else
            {
                var overdueDays =
                    Math.Abs(daysFromDueDate);

                message =
                    $"Rent overdue: ₹{remaining} is outstanding. " +
                    $"Due date was {rent.DueDate:dd MMM yyyy}. " +
                    $"Overdue by {overdueDays} day{(overdueDays == 1 ? "" : "s")}.";
            }

            var notification = new Notification
            {
                TenantId = rent.TenantId,
                RentId = rent.Id,
                Type = reminderType,
                Channel = "WhatsApp",
                Message = message,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.Notifications.Add(notification);

            created++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return created;
    }
}