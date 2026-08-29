using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
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
        var today = IndiaClock.Today();

        var rents = await _db.Rents
            .Include(r => r.Tenant)
            .Where(r => !r.IsSettled && r.Tenant.IsActive)
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var rent in rents)
        {
            var daysFromDueDate = IndiaClock.DaysBetween(today, rent.DueDate);

            string? reminderType = daysFromDueDate switch
            {
                // Three moments, not seven. A landlord who gets a push
                // every day for the same tenant stops reading them.
                3 => "RentDue3Days",
                0 => "RentDueToday",
                -7 => "RentOverdue7Days",
                _ => null
            };

            if (reminderType is null)
            {
                continue;
            }

            var alreadyCreated = await _db.Notifications
                .AnyAsync(
                    n => n.RentId == rent.Id && n.Type == reminderType,
                    cancellationToken);

            if (alreadyCreated)
            {
                continue;
            }

            var remaining = Math.Max(0m, rent.AmountDue - rent.AmountPaid);
            var dueDateText = rent.DueDate.ToString("dd MMM yyyy");
            var overdueDays = Math.Abs(daysFromDueDate);

            var message = daysFromDueDate switch
            {
                > 0 => $"Rent reminder: Rs {remaining:N0} is due on {dueDateText}. " +
                       "Please make the payment by the due date.",
                0 => $"Rent reminder: Rs {remaining:N0} is due today.",
                _ => $"Rent overdue: Rs {remaining:N0} is outstanding. " +
                     $"Due date was {dueDateText}. Overdue by {overdueDays} " +
                     $"day{(overdueDays == 1 ? "" : "s")}."
            };

            _db.Notifications.Add(new Notification
            {
                TenantId = rent.TenantId,
                RentId = rent.Id,
                Type = reminderType,
                Channel = "WhatsApp",
                Message = message,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            });

            created++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return created;
    }
}
