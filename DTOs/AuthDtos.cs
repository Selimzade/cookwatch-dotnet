using System.ComponentModel.DataAnnotations;

namespace CookWatch.API.DTOs;

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(30)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username may only contain letters, numbers, and underscores.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public string? DisplayName { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ShareId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class QrCodeResponse
{
    public string QrCode { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
}
