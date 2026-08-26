using Microsoft.AspNetCore.Mvc;
using RentManager.Api.Services;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public class ReminderController : ControllerBase
{
    private readonly RentReminderService _reminderService;

    public ReminderController(
        RentReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReminders(
        CancellationToken cancellationToken)
    {
        var created =
            await _reminderService.GenerateRemindersAsync(
                cancellationToken);

        return Ok(new
        {
            Created = created
        });
    }
}