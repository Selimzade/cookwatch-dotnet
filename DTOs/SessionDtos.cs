using System.ComponentModel.DataAnnotations;

namespace CookWatch.API.DTOs;

public class CreateSessionRequest
{
    [Required]
    public string MealId { get; set; } = string.Empty;

    [Range(1, 720)]
    public int? Duration { get; set; }

    public string? SessionDate { get; set; }
}

public class UpdateSessionDurationRequest
{
    [Required]
    [Range(1, 720)]
    public int Duration { get; set; }
}

public class SessionDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MealId { get; set; } = string.Empty;
    public string MealName { get; set; } = string.Empty;
    public string MealDescription { get; set; } = string.Empty;
    public string MealImage { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? RemainingSeconds { get; set; }
    public string SessionDate { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
