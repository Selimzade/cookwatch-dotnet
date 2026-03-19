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
[Route("api/menus")]
[Authorize]
public class MenusController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly BroadcastService _broadcastService;

    public MenusController(MongoDbService db, BroadcastService broadcastService)
    {
        _db = db;
        _broadcastService = broadcastService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMenus()
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var menus = await _db.Menus
            .Find(m => m.UserId == userId && m.Date == today)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        return Ok(menus.Select(BroadcastService.MapMenu));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var today = request.Date ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var menu = new Menu
        {
            UserId = userId,
            Name = request.Name,
            Date = today,
            IsAccepting = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Menus.InsertOneAsync(menu);

        if (shareId != null)
            await _broadcastService.BroadcastMenusAsync(shareId, userId);

        return CreatedAtAction(null, null, BroadcastService.MapMenu(menu));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMenu(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        var menu = await _db.Menus.Find(m => m.Id == id && m.UserId == userId).FirstOrDefaultAsync();
        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        // Cascade delete menu items
        await _db.MenuItems.DeleteManyAsync(mi => mi.MenuId == id);

        await _db.Menus.DeleteOneAsync(m => m.Id == id);

        if (shareId != null)
            await _broadcastService.BroadcastMenusAsync(shareId, userId);

        return Ok(new { message = "Menu deleted." });
    }

    [HttpGet("{menuId}/items")]
    public async Task<IActionResult> GetMenuItems(string menuId)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(menuId, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        // Verify user owns menu
        var menu = await _db.Menus.Find(m => m.Id == menuId && m.UserId == userId).FirstOrDefaultAsync();
        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        var items = await _db.MenuItems
            .Find(mi => mi.MenuId == menuId)
            .SortBy(mi => mi.CreatedAt)
            .ToListAsync();

        return Ok(items.Select(BroadcastService.MapMenuItem));
    }

    [HttpPost("{menuId}/items")]
    public async Task<IActionResult> AddMenuItem(string menuId, [FromBody] AddMenuItemRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(menuId, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        if (!ObjectId.TryParse(request.MealId, out _))
            return BadRequest(new { error = "Invalid meal ID." });

        // Verify user owns menu
        var menu = await _db.Menus.Find(m => m.Id == menuId && m.UserId == userId).FirstOrDefaultAsync();
        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        if (!menu.IsAccepting)
            return BadRequest(new { error = "Menu is not accepting new items." });

        // Find the meal
        var meal = await _db.Meals.Find(m => m.Id == request.MealId && m.UserId == userId).FirstOrDefaultAsync();
        if (meal == null)
            return NotFound(new { error = "Meal not found." });

        // Check no duplicate mealId in this menu
        var existing = await _db.MenuItems
            .Find(mi => mi.MenuId == menuId && mi.MealId == request.MealId)
            .FirstOrDefaultAsync();
        if (existing != null)
            return Conflict(new { error = "This meal is already in the menu." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var item = new MenuItem
        {
            MenuId = menuId,
            UserId = userId,
            MealId = meal.Id,
            MealName = meal.Name,
            MealDescription = meal.Description,
            MealImage = meal.Image,
            DefaultDuration = meal.DefaultDuration,
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.MenuItems.InsertOneAsync(item);

        if (shareId != null)
            await _broadcastService.BroadcastMenuItemsAsync(shareId, menuId);

        return CreatedAtAction(null, null, BroadcastService.MapMenuItem(item));
    }

    [HttpDelete("{menuId}/items/{itemId}")]
    public async Task<IActionResult> RemoveMenuItem(string menuId, string itemId)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(menuId, out _))
            return BadRequest(new { error = "Invalid menu ID." });

        if (!ObjectId.TryParse(itemId, out _))
            return BadRequest(new { error = "Invalid item ID." });

        // Verify user owns menu
        var menu = await _db.Menus.Find(m => m.Id == menuId && m.UserId == userId).FirstOrDefaultAsync();
        if (menu == null)
            return NotFound(new { error = "Menu not found." });

        if (!menu.IsAccepting)
            return BadRequest(new { error = "Menu is not accepting changes." });

        var result = await _db.MenuItems.DeleteOneAsync(mi => mi.Id == itemId && mi.MenuId == menuId);
        if (result.DeletedCount == 0)
            return NotFound(new { error = "Menu item not found." });

        if (shareId != null)
            await _broadcastService.BroadcastMenuItemsAsync(shareId, menuId);

        return Ok(new { message = "Menu item removed." });
    }

    private (string? userId, string? shareId) GetUserInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var shareId = User.FindFirst("shareId")?.Value;
        return (userId, shareId);
    }
}
