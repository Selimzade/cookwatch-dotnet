using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using CookWatch.API.Hubs;
using CookWatch.API.Models;
using CookWatch.API.DTOs;

namespace CookWatch.API.Services;

public class BroadcastService
{
    private readonly IHubContext<CookWatchHub> _hubContext;
    private readonly MongoDbService _db;

    public BroadcastService(IHubContext<CookWatchHub> hubContext, MongoDbService db)
    {
        _hubContext = hubContext;
        _db = db;
    }

    public async Task BroadcastMenusAsync(string shareId, string userId)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var menus = await _db.Menus
            .Find(m => m.UserId == userId && m.Date == today)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        var menuDtos = menus.Select(MapMenu).ToList();
        await _hubContext.Clients.Group($"user:{shareId}").SendAsync("menus:update", new { menus = menuDtos });
    }

    public async Task BroadcastMenuItemsAsync(string shareId, string menuId)
    {
        var items = await _db.MenuItems
            .Find(mi => mi.MenuId == menuId)
            .SortBy(mi => mi.CreatedAt)
            .ToListAsync();

        var itemDtos = items.Select(MapMenuItem).ToList();
        await _hubContext.Clients.Group($"user:{shareId}").SendAsync("menu:items:update", new { menuId, items = itemDtos });
    }

    public async Task BroadcastOrdersAsync(string shareId, string userId)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var orders = await _db.Orders
            .Find(o => o.UserId == userId && o.Date == today)
            .SortBy(o => o.CreatedAt)
            .ToListAsync();

        var orderDtos = orders.Select(MapOrder).ToList();
        await _hubContext.Clients.Group($"user:{shareId}").SendAsync("orders:update", new { orders = orderDtos });
    }

    public async Task BroadcastSessionsAsync(string shareId, string userId)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var sessions = await _db.CookingSessions
            .Find(s => s.UserId == userId && s.SessionDate == today)
            .SortBy(s => s.Order)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync();

        var sessionDtos = sessions.Select(MapSession).ToList();
        await _hubContext.Clients.Group($"user:{shareId}").SendAsync("sessions:update", new { sessions = sessionDtos });
    }

    public static MenuDto MapMenu(Menu m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        Name = m.Name,
        Date = m.Date,
        IsAccepting = m.IsAccepting,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    public static MenuItemDto MapMenuItem(MenuItem mi) => new()
    {
        Id = mi.Id,
        MenuId = mi.MenuId,
        UserId = mi.UserId,
        MealId = mi.MealId,
        MealName = mi.MealName,
        MealDescription = mi.MealDescription,
        MealImage = mi.MealImage,
        DefaultDuration = mi.DefaultDuration,
        Date = mi.Date,
        CreatedAt = mi.CreatedAt,
        UpdatedAt = mi.UpdatedAt
    };

    public static OrderDto MapOrder(Order o) => new()
    {
        Id = o.Id,
        UserId = o.UserId,
        MenuId = o.MenuId,
        MenuItemId = o.MenuItemId,
        MealName = o.MealName,
        MealDescription = o.MealDescription,
        MealImage = o.MealImage,
        Status = o.Status.ToString().ToLower(),
        Duration = o.Duration,
        StartTime = o.StartTime,
        EndTime = o.EndTime,
        RemainingSeconds = o.RemainingSeconds,
        Date = o.Date,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };

    public static SessionDto MapSession(CookingSession s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        MealId = s.MealId,
        MealName = s.MealName,
        MealDescription = s.MealDescription,
        MealImage = s.MealImage,
        Duration = s.Duration,
        Status = SessionStatusToString(s.Status),
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        RemainingSeconds = s.RemainingSeconds,
        SessionDate = s.SessionDate,
        Order = s.Order,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    public static string SessionStatusToString(SessionStatus status) => status switch
    {
        SessionStatus.NotStarted => "not_started",
        SessionStatus.Cooking => "cooking",
        SessionStatus.Completed => "completed",
        SessionStatus.Cancelled => "cancelled",
        _ => "not_started"
    };
}
