using System.ComponentModel.DataAnnotations;

namespace CookWatch.API.DTOs;

public class CreateMealRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(1, 720)]
    public int DefaultDuration { get; set; } = 30;

    public string Image { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();
}

public class UpdateMealRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Range(1, 720)]
    public int? DefaultDuration { get; set; }

    public string? Image { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public List<string>? Tags { get; set; }
}

public class MealDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DefaultDuration { get; set; }
    public int TimesCooked { get; set; }
    public DateTime? LastCookedAt { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AiDescribeRequest
{
    public string Image { get; set; } = string.Empty;
}

public class AiDescribeResponse
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class AiDurationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AiDurationResponse
{
    public int Duration { get; set; } = 30;
}
