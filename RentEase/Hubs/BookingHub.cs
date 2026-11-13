using Microsoft.AspNetCore.SignalR;

namespace RentEase.Hubs;

public class BookingHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.ToLowerInvariant()}");
        }
    }

    public async Task LeaveUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId.ToLowerInvariant()}");
        }
    }
}