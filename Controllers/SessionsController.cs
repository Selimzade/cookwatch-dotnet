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
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly BroadcastService _broadcastService;

    public SessionsController(MongoDbService db, BroadcastService broadcastService)
    {
        _db = db;
        _broadcastService = broadcastService;
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetTodaySessions()
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Auto-complete expired sessions
        await AutoCompleteExpiredSessionsAsync(userId);

        var sessions = await _db.CookingSessions
            .Find(s => s.UserId == userId && s.SessionDate == today)
            .SortBy(s => s.Order)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sessions.Select(BroadcastService.MapSession));
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] string? date, [FromQuery] int? limit)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        var filterBuilder = Builders<CookingSession>.Filter;
        var filter = filterBuilder.Eq(s => s.UserId, userId);

        if (!string.IsNullOrEmpty(date))
            filter &= filterBuilder.Eq(s => s.SessionDate, date);

        var sortedQuery = _db.CookingSessions
            .Find(filter)
            .SortByDescending(s => s.SessionDate)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.CreatedAt);

        var sessions = limit.HasValue && limit.Value > 0
            ? await sortedQuery.Limit(limit.Value).ToListAsync()
            : await sortedQuery.ToListAsync();
        return Ok(sessions.Select(BroadcastService.MapSession));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(request.MealId, out _))
            return BadRequest(new { error = "Invalid meal ID." });

        // Find meal
        var meal = await _db.Meals.Find(m => m.Id == request.MealId && m.UserId == userId).FirstOrDefaultAsync();
        if (meal == null)
            return NotFound(new { error = "Meal not found." });

        var sessionDate = request.SessionDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Count existing sessions for ordering
        var existingCount = await _db.CookingSessions
            .CountDocumentsAsync(s => s.UserId == userId && s.SessionDate == sessionDate);

        var session = new CookingSession
        {
            UserId = userId,
            MealId = meal.Id,
            MealName = meal.Name,
            MealDescription = meal.Description,
            MealImage = meal.Image,
            Duration = request.Duration ?? meal.DefaultDuration,
            Status = SessionStatus.NotStarted,
            SessionDate = sessionDate,
            Order = (int)existingCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.CookingSessions.InsertOneAsync(session);

        if (shareId != null)
            await _broadcastService.BroadcastSessionsAsync(shareId, userId);

        return CreatedAtAction(null, null, BroadcastService.MapSession(session));
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartSession(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid session ID." });

        var session = await _db.CookingSessions
            .Find(s => s.Id == id && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.Status != SessionStatus.NotStarted)
            return BadRequest(new { error = "Session is not in not_started status." });

        var now = DateTime.UtcNow;
        var endTime = now.AddSeconds(session.Duration * 60);

        var update = Builders<CookingSession>.Update
            .Set(s => s.Status, SessionStatus.Cooking)
            .Set(s => s.StartTime, now)
            .Set(s => s.EndTime, endTime)
            .Set(s => s.UpdatedAt, now);

        var updated = await _db.CookingSessions.FindOneAndUpdateAsync(
            s => s.Id == id && s.UserId == userId,
            update,
            new FindOneAndUpdateOptions<CookingSession> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Session not found." });

        // Increment meal.timesCooked and update lastCookedAt
        await _db.Meals.UpdateOneAsync(
            m => m.Id == session.MealId,
            Builders<Meal>.Update
                .Inc(m => m.TimesCooked, 1)
                .Set(m => m.LastCookedAt, now)
                .Set(m => m.UpdatedAt, now));

        if (shareId != null)
            await _broadcastService.BroadcastSessionsAsync(shareId, userId);

        return Ok(BroadcastService.MapSession(updated));
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteSession(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid session ID." });

        var now = DateTime.UtcNow;
        var update = Builders<CookingSession>.Update
            .Set(s => s.Status, SessionStatus.Completed)
            .Set(s => s.UpdatedAt, now);

        var updated = await _db.CookingSessions.FindOneAndUpdateAsync(
            s => s.Id == id && s.UserId == userId && s.Status == SessionStatus.Cooking,
            update,
            new FindOneAndUpdateOptions<CookingSession> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Session not found or not in cooking status." });

        if (shareId != null)
            await _broadcastService.BroadcastSessionsAsync(shareId, userId);

        return Ok(BroadcastService.MapSession(updated));
    }

    [HttpPatch("{id}/duration")]
    public async Task<IActionResult> UpdateSessionDuration(string id, [FromBody] UpdateSessionDurationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid session ID." });

        var session = await _db.CookingSessions
            .Find(s => s.Id == id && s.UserId == userId)
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound(new { error = "Session not found." });

        var now = DateTime.UtcNow;
        var updateDef = Builders<CookingSession>.Update
            .Set(s => s.Duration, request.Duration)
            .Set(s => s.UpdatedAt, now);

        // If cooking, also update endTime
        if (session.Status == SessionStatus.Cooking && session.StartTime.HasValue)
        {
            var newEndTime = session.StartTime.Value.AddSeconds(request.Duration * 60);
            updateDef = updateDef.Set(s => s.EndTime, newEndTime);
        }

        var updated = await _db.CookingSessions.FindOneAndUpdateAsync(
            s => s.Id == id && s.UserId == userId,
            updateDef,
            new FindOneAndUpdateOptions<CookingSession> { ReturnDocument = ReturnDocument.After });

        if (updated == null)
            return NotFound(new { error = "Session not found." });

        if (shareId != null)
            await _broadcastService.BroadcastSessionsAsync(shareId, userId);

        return Ok(BroadcastService.MapSession(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSession(string id)
    {
        var (userId, shareId) = GetUserInfo();
        if (userId == null) return Unauthorized(new { error = "Unauthorized." });

        if (!ObjectId.TryParse(id, out _))
            return BadRequest(new { error = "Invalid session ID." });

        var result = await _db.CookingSessions.DeleteOneAsync(s => s.Id == id && s.UserId == userId);
        if (result.DeletedCount == 0)
            return NotFound(new { error = "Session not found." });

        if (shareId != null)
            await _broadcastService.BroadcastSessionsAsync(shareId, userId);

        return Ok(new { message = "Session deleted." });
    }

    private async Task AutoCompleteExpiredSessionsAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<CookingSession>.Filter.And(
            Builders<CookingSession>.Filter.Eq(s => s.UserId, userId),
            Builders<CookingSession>.Filter.Eq(s => s.Status, SessionStatus.Cooking),
            Builders<CookingSession>.Filter.Lte(s => s.EndTime, now)
        );

        var update = Builders<CookingSession>.Update
            .Set(s => s.Status, SessionStatus.Completed)
            .Set(s => s.UpdatedAt, now);

        await _db.CookingSessions.UpdateManyAsync(filter, update);
    }

    private (string? userId, string? shareId) GetUserInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var shareId = User.FindFirst("shareId")?.Value;
        return (userId, shareId);
    }
}
