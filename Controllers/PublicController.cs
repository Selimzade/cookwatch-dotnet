using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using CookWatch.API.DTOs;
using CookWatch.API.Models;
using CookWatch.API.Services;

namespace CookWatch.API.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly BroadcastService _broadcastService;

    public PublicController(MongoDbService db, BroadcastService broadcastService)
    {
        _db = db;
        _broadcastService = broadcastService;
    }

    [HttpGet("view/{shareId}")]
    public async Task<IActionResult> GetPublicView(string shareId)
    {
        if (!Guid.TryParse(shareId, out _))
            return BadRequest(new { error = "Invalid shareId." });

        var user = await _db.Users.Find(u => u.ShareId == shareId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { error = "User not found." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var menus = await _db.Menus
            .Find(m => m.UserId == user.Id && m.Date == today)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(new PublicViewResponse
        {
            User = new PublicUserDto
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                ShareId = user.ShareId
            },
            Date = today,
            Menus = menus.Select(BroadcastService.MapMenu).ToList(),
            ServerTime = DateTime.UtcNow
        });
    }

    [HttpGet("{shareId}/menu/{menuId}")]
    public async Task<IActionResult> GetPublicMenu(string shareId, string menuId)
    {
        if (!Guid.TryParse(shareId, out _))
            return BadRequest(new { error = "Invalid shareId." });

        if (!ObjectId.TryParse(menuId, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        var user = await _db.Users.Find(u => u.ShareId == shareId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { error = "User not found." });

        // Auto-complete expired orders
        await AutoCompleteExpiredOrdersAsync(user.Id);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var menu = await _db.Menus
            .Find(m => m.Id == menuId && m.UserId == user.Id && m.Date == today)
            .FirstOrDefaultAsync();

        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        var items = await _db.MenuItems
            .Find(mi => mi.MenuId == menuId)
            .SortBy(mi => mi.CreatedAt)
            .ToListAsync();

        var orders = await _db.Orders
            .Find(o => o.MenuId == menuId && o.Date == today)
            .SortBy(o => o.CreatedAt)
            .ToListAsync();

        return Ok(new PublicMenuResponse
        {
            Menu = BroadcastService.MapMenu(menu),
            Items = items.Select(BroadcastService.MapMenuItem).ToList(),
            Orders = orders.Select(BroadcastService.MapOrder).ToList()
        });
    }

    [HttpPost("{shareId}/menu/{menuId}/order")]
    public async Task<IActionResult> PlaceOrder(string shareId, string menuId, [FromBody] PlaceOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        if (!Guid.TryParse(shareId, out _))
            return BadRequest(new { error = "Invalid shareId." });

        if (!ObjectId.TryParse(menuId, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        if (!ObjectId.TryParse(request.MenuItemId, out _))
            return BadRequest(new { error = "Invalid menu item ID." });

        var user = await _db.Users.Find(u => u.ShareId == shareId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { error = "User not found." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var menu = await _db.Menus
            .Find(m => m.Id == menuId && m.UserId == user.Id && m.Date == today)
            .FirstOrDefaultAsync();

        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        if (!menu.IsAccepting)
            return BadRequest(new { error = "Menu is not accepting orders." });

        // Find the menu item
        var menuItem = await _db.MenuItems
            .Find(mi => mi.Id == request.MenuItemId && mi.MenuId == menuId)
            .FirstOrDefaultAsync();

        if (menuItem == null)
            return NotFound(new { error = "Menu item not found." });

        // Check no existing order for this menuItemId+date (any status)
        var existingOrder = await _db.Orders
            .Find(o => o.MenuItemId == request.MenuItemId && o.Date == today)
            .FirstOrDefaultAsync();

        if (existingOrder != null)
            return Conflict(new { error = "An order for this item already exists." });

        var order = new Order
        {
            UserId = user.Id,
            MenuId = menuId,
            MenuItemId = menuItem.Id,
            MealName = menuItem.MealName,
            MealDescription = menuItem.MealDescription,
            MealImage = menuItem.MealImage,
            Status = OrderStatus.Pending,
            Duration = menuItem.DefaultDuration,
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Orders.InsertOneAsync(order);

        // Broadcast via SignalR
        await _broadcastService.BroadcastOrdersAsync(shareId, user.Id);

        return CreatedAtAction(null, null, BroadcastService.MapOrder(order));
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
}
