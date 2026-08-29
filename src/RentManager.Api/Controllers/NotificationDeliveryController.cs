using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentManager.Api.Services;

namespace RentManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notification-delivery")]
public class NotificationDeliveryController : ControllerBase
{
    private readonly NotificationDeliveryService _deliveryService;

    public NotificationDeliveryController(
        NotificationDeliveryService deliveryService)
    {
        _deliveryService = deliveryService;
    }

    [HttpPost("process")]
    public async Task<IActionResult> Process(
        CancellationToken cancellationToken)
    {
        var processed =
            await _deliveryService
                .ProcessPendingNotificationsAsync(
                    cancellationToken);

        return Ok(new
        {
            processed
        });
    }
}