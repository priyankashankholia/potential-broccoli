namespace RentManager.Api.Models;

// One row per browser install, not per user.
public class PushSubscription
{
    public int Id { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 404 or 410 means gone. Other errors are counted, so a transient
    // outage does not silently unsubscribe a device.
    public int FailureCount { get; set; }
    public DateTime? LastSentAt { get; set; }
}
