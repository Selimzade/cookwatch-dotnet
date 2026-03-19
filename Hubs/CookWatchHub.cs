using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using CookWatch.API.Models;
using CookWatch.API.Services;
using CookWatch.API.DTOs;

namespace CookWatch.API.Hubs;

public class CookWatchHub : Hub
{
    private readonly TokenService _tokenService;
    private readonly MongoDbService _db;
    private readonly BroadcastService _broadcastService;
    private readonly ILogger<CookWatchHub> _logger;

    public CookWatchHub(
        TokenService tokenService,
        MongoDbService db,
        BroadcastService broadcastService,
        ILogger<CookWatchHub> logger)
    {
        _tokenService = tokenService;
        _db = db;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public async Task AuthJoin(string token)
    {
        var principal = _tokenService.ValidateToken(token);
        if (principal == null)
        {
            await Clients.Caller.SendAsync("error", new { message = "Invalid token" });
            return;
        }

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        var shareId = principal.FindFirst("shareId")?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(shareId))
        {
            await Clients.Caller.SendAsync("error", new { message = "Invalid token claims" });
            return;
        }

        var groupName = $"user:{shareId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Caller.SendAsync("auth:joined", new { shareId });
        _logger.LogInformation("Client {ConnectionId} joined auth group {Group}", Context.ConnectionId, groupName);
    }

    public async Task ViewJoin(string shareId)
    {
        // Validate UUID format
        if (!Guid.TryParse(shareId, out _))
        {
            await Clients.Caller.SendAsync("error", new { message = "Invalid shareId format" });
            return;
        }

        // Find user by shareId to confirm they exist
        var user = await _db.Users.Find(u => u.ShareId == shareId).FirstOrDefaultAsync();
        if (user == null)
        {
            await Clients.Caller.SendAsync("error", new { message = "User not found" });
            return;
        }

        var groupName = $"user:{shareId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Auto-complete expired orders before sending
        await AutoCompleteExpiredOrdersAsync(user.Id);

        // Send current menus
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var menus = await _db.Menus
            .Find(m => m.UserId == user.Id && m.Date == today)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();
        var menuDtos = menus.Select(BroadcastService.MapMenu).ToList();
        await Clients.Caller.SendAsync("menus:update", new { menus = menuDtos });

        // Send current orders
        var orders = await _db.Orders
            .Find(o => o.UserId == user.Id && o.Date == today)
            .SortBy(o => o.CreatedAt)
            .ToListAsync();
        var orderDtos = orders.Select(BroadcastService.MapOrder).ToList();
        await Clients.Caller.SendAsync("orders:update", new { orders = orderDtos });

        _logger.LogInformation("Client {ConnectionId} joined view group {Group}", Context.ConnectionId, groupName);
    }

    private async Task AutoCompleteExpiredOrdersAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<Order>.Filter.And(
            Builders<Order>.Filter.Eq(o => o.UserId, userId),
            Builders<Order>.Filter.Eq(o => o.Status, OrderStatus.Cooking),
            Builders<Order>.Filter.Lte(o => o.EndTime, now)
        );

        var update = Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Completed)
            .Set(o => o.UpdatedAt, now);

        await _db.Orders.UpdateManyAsync(filter, update);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Background service that auto-completes expired cooking orders every 30 seconds.
/// </summary>
public class OrderAutoCompleteService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderAutoCompleteService> _logger;

    public OrderAutoCompleteService(IServiceProvider serviceProvider, ILogger<OrderAutoCompleteService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await ProcessExpiredOrdersAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in order auto-complete service");
            }
        }
    }

    private async Task ProcessExpiredOrdersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<CookWatchHub>>();

        var now = DateTime.UtcNow;
        var filter = Builders<Order>.Filter.And(
            Builders<Order>.Filter.Eq(o => o.Status, OrderStatus.Cooking),
            Builders<Order>.Filter.Lte(o => o.EndTime, now)
        );

        // Find affected user/shareId combos before updating
        var expiredOrders = await db.Orders.Find(filter).ToListAsync();
        if (!expiredOrders.Any()) return;

        var update = Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Completed)
            .Set(o => o.UpdatedAt, now);

        await db.Orders.UpdateManyAsync(filter, update);
        _logger.LogInformation("Auto-completed {Count} expired orders", expiredOrders.Count);

        // Broadcast updates per user
        var userIds = expiredOrders.Select(o => o.UserId).Distinct();
        foreach (var userId in userIds)
        {
            var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) continue;

            var today = now.ToString("yyyy-MM-dd");
            var orders = await db.Orders
                .Find(o => o.UserId == userId && o.Date == today)
                .SortBy(o => o.CreatedAt)
                .ToListAsync();

            var orderDtos = orders.Select(BroadcastService.MapOrder).ToList();
            await hubContext.Clients.Group($"user:{user.ShareId}").SendAsync("orders:update", new { orders = orderDtos });
        }
    }
}
