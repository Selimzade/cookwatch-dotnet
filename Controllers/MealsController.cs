using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CookWatch.API.DTOs;
using CookWatch.API.Models;
using CookWatch.API.Services;

namespace CookWatch.API.Controllers;

[ApiController]
[Route("api/meals")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly MongoDbService _db;

    public MealsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMeals([FromQuery] string? search, [FromQuery] string? category, [FromQuery] string? sort)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var filterBuilder = Builders<Meal>.Filter;
        var filter = filterBuilder.Eq(m => m.UserId, userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(search, "i");
            filter &= filterBuilder.Regex(m => m.Name, regex);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filter &= filterBuilder.Eq(m => m.Category, category);
        }

        var sortDef = Builders<Meal>.Sort.Descending(m => m.TimesCooked);

        var meals = await _db.Meals.Find(filter).Sort(sortDef).ToListAsync();
        return Ok(meals.Select(MapMeal));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var filter = Builders<Meal>.Filter.And(
            Builders<Meal>.Filter.Eq(m => m.UserId, userId),
            Builders<Meal>.Filter.Ne(m => m.Category, "")
        );

        var categories = await _db.Meals
            .Distinct(m => m.Category, filter)
            .ToListAsync();

        categories.Sort(StringComparer.OrdinalIgnoreCase);
        return Ok(categories);
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var meals = await _db.Meals
            .Find(m => m.UserId == userId)
            .SortByDescending(m => m.TimesCooked)
            .Limit(5)
            .ToListAsync();

        return Ok(meals.Select(MapMeal));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMeal(string id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid meal ID." });

        var meal = await _db.Meals.Find(m => m.Id == id && m.UserId == userId).FirstOrDefaultAsync();
        if (meal == null)
            return NotFound(new { error = "Meal not found." });

        return Ok(MapMeal(meal));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMeal([FromBody] CreateMealRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        // Check for duplicate name (case-insensitive)
        var regex = new BsonRegularExpression($"^{Regex.Escape(request.Name)}$", "i");
        var existing = await _db.Meals
            .Find(m => m.UserId == userId)
            .ToListAsync();
        var duplicate = existing.FirstOrDefault(m => string.Equals(m.Name, request.Name, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
            return Conflict(new { error = "A meal with this name already exists." });

        var meal = new Meal
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            DefaultDuration = request.DefaultDuration,
            Image = request.Image,
            Category = request.Category,
            Tags = request.Tags,
            TimesCooked = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Meals.InsertOneAsync(meal);
        return CreatedAtAction(nameof(GetMeal), new { id = meal.Id }, MapMeal(meal));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateMeal(string id, [FromBody] UpdateMealRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid meal ID." });

        var meal = await _db.Meals.Find(m => m.Id == id && m.UserId == userId).FirstOrDefaultAsync();
        if (meal == null)
            return NotFound(new { error = "Meal not found." });

        // Check duplicate name if name is being changed
        if (!string.IsNullOrEmpty(request.Name) && !string.Equals(request.Name, meal.Name, StringComparison.OrdinalIgnoreCase))
        {
            var all = await _db.Meals.Find(m => m.UserId == userId && m.Id != id).ToListAsync();
            var duplicate = all.FirstOrDefault(m => string.Equals(m.Name, request.Name, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
                return Conflict(new { error = "A meal with this name already exists." });
        }

        var updateDef = Builders<Meal>.Update.Set(m => m.UpdatedAt, DateTime.UtcNow);

        if (request.Name != null) updateDef = updateDef.Set(m => m.Name, request.Name);
        if (request.Description != null) updateDef = updateDef.Set(m => m.Description, request.Description);
        if (request.DefaultDuration.HasValue) updateDef = updateDef.Set(m => m.DefaultDuration, request.DefaultDuration.Value);
        if (request.Image != null) updateDef = updateDef.Set(m => m.Image, request.Image);
        if (request.Category != null) updateDef = updateDef.Set(m => m.Category, request.Category);
        if (request.Tags != null) updateDef = updateDef.Set(m => m.Tags, request.Tags);

        var updated = await _db.Meals.FindOneAndUpdateAsync(
            m => m.Id == id && m.UserId == userId,
            updateDef,
            new FindOneAndUpdateOptions<Meal> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Meal not found." });

        return Ok(MapMeal(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeal(string id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid meal ID." });

        var result = await _db.Meals.DeleteOneAsync(m => m.Id == id && m.UserId == userId);
        if (result.DeletedCount == 0)
            return NotFound(new { error = "Meal not found." });

        return Ok(new { message = "Meal deleted." });
    }

    [HttpPost("ai/describe")]
    public IActionResult AiDescribe([FromBody] AiDescribeRequest request)
    {
        // AI not available in .NET version - return defaults
        return Ok(new AiDescribeResponse
        {
            Name = "",
            Description = "",
            Category = "",
            Tags = new List<string>()
        });
    }

    [HttpPost("ai/duration")]
    public IActionResult AiDuration([FromBody] AiDurationRequest request)
    {
        // AI not available in .NET version - return default duration
        return Ok(new AiDurationResponse { Duration = 30 });
    }

    private string? GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    public static MealDto MapMeal(Meal m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        Name = m.Name,
        Description = m.Description,
        DefaultDuration = m.DefaultDuration,
        TimesCooked = m.TimesCooked,
        LastCookedAt = m.LastCookedAt,
        Image = m.Image,
        Category = m.Category,
        Tags = m.Tags,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
