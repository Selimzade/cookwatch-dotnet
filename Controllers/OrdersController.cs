using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using CookWatch.API.DTOs;
using CookWatch.API.Models;
using CookWatch.API.Services;

namespace CookWatch.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly BroadcastService _broadcastService;

    public OrdersController(MongoDbService db, BroadcastService broadcastService)
    {
        _db = db;
        _broadcastService = broadcastService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        // Auto-complete expired cooking orders
        await AutoCompleteExpiredOrdersAsync(userId);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var orders = await _db.Orders
            .Find(o => o.UserId == userId && o.Date == today)
            .SortBy(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(BroadcastService.MapOrder));
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartOrder(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid order ID." });

        var order = await _db.Orders.Find(o => o.Id == id && o.UserId == userId).FirstOrDefaultAsync();
        if (order == null)
            return NotFound(new { error = "Order not found." });

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { error = "Order is not in pending status." });

        var now = DateTime.UtcNow;
        var endTime = now.AddSeconds(order.Duration * 60);

        var update = Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Cooking)
            .Set(o => o.StartTime, now)
            .Set(o => o.EndTime, endTime)
            .Set(o => o.UpdatedAt, now);

        var updated = await _db.Orders.FindOneAndUpdateAsync(
            o => o.Id == id && o.UserId == userId,
            update,
            new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Order not found." });

        // Set menu.isAccepting = false
        await _db.Menus.UpdateOneAsync(
            m => m.Id == order.MenuId,
            Builders<Menu>.Update
                .Set(m => m.IsAccepting, false)
                .Set(m => m.UpdatedAt, now));

        if (shareId != null)
        {
            await _broadcastService.BroadcastMenusAsync(shareId, userId);
            await _broadcastService.BroadcastOrdersAsync(shareId, userId);
        }

        return Ok(BroadcastService.MapOrder(updated));
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteOrder(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid order ID." });

        var now = DateTime.UtcNow;
        var update = Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Completed)
            .Set(o => o.UpdatedAt, now);

        var updated = await _db.Orders.FindOneAndUpdateAsync(
            o => o.Id == id && o.UserId == userId && o.Status == OrderStatus.Cooking,
            update,
            new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Order not found or not in cooking status." });

        if (shareId != null)
            await _broadcastService.BroadcastOrdersAsync(shareId, userId);

        return Ok(BroadcastService.MapOrder(updated));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid order ID." });

        var now = DateTime.UtcNow;
        var update = Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Cancelled)
            .Set(o => o.UpdatedAt, now);

        var updated = await _db.Orders.FindOneAndUpdateAsync(
            o => o.Id == id && o.UserId == userId,
            update,
            new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Order not found." });

        if (shareId != null)
            await _broadcastService.BroadcastOrdersAsync(shareId, userId);

        return Ok(BroadcastService.MapOrder(updated));
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

    private (string? userId, string? shareId) GetUserInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var shareId = User.FindFirst("shareId")?.Value;
        return (userId, shareId);
    }
}
