using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using QRCoder;
using System.Drawing;
using System.Security.Claims;
using CookWatch.API.DTOs;
using CookWatch.API.Models;
using CookWatch.API.Services;

namespace CookWatch.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(MongoDbService db, TokenService tokenService, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var email = request.Email.ToLowerInvariant();

        // Check for existing email
        var existingEmail = await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (existingEmail != null)
            return Conflict(new { error = "Email already in use." });

        // Check for existing username
        var existingUsername = await _db.Users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();
        if (existingUsername != null)
            return Conflict(new { error = "Username already taken." });

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = new User
        {
            Username = request.Username,
            Email = email,
            Password = hashedPassword,
            ShareId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Users.InsertOneAsync(user);

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponse
        {
            Token = token,
            User = MapUser(user)
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        var email = request.Email.ToLowerInvariant();
        var user = await _db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(new { error = "Invalid email or password." });

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponse
        {
            Token = token,
            User = MapUser(user)
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Unauthorized." });

        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { error = "User not found." });

        return Ok(MapUser(user));
    }

    [HttpGet("qrcode")]
    [Authorize]
    public async Task<IActionResult> GetQrCode()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Unauthorized." });

        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { error = "User not found." });

        var baseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
            ?? _configuration["App:BaseUrl"]
            ?? "http://localhost:5173";

        var shareUrl = $"{baseUrl}/view/{user.ShareId}";

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(shareUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        var base64 = Convert.ToBase64String(qrCodeBytes);
        var dataUrl = $"data:image/png;base64,{base64}";

        return Ok(new QrCodeResponse
        {
            QrCode = dataUrl,
            ShareUrl = shareUrl
        });
    }

    [HttpPatch("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Unauthorized." });

        var update = Builders<User>.Update
            .Set(u => u.DisplayName, request.DisplayName)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var user = await _db.Users.FindOneAndUpdateAsync(
            u => u.Id == userId,
            update,
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });

        if (user == null)
            return NotFound(new { error = "User not found." });

        return Ok(MapUser(user));
    }

    private string? GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        ShareId = user.ShareId,
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
