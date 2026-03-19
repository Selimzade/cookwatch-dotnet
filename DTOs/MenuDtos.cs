using System.ComponentModel.DataAnnotations;

namespace CookWatch.API.DTOs;

public class CreateMenuRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Date { get; set; }
}

public class MenuDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public bool IsAccepting { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AddMenuItemRequest
{
    [Required]
    public string MealId { get; set; } = string.Empty;
}

public class MenuItemDto
{
    public string Id { get; set; } = string.Empty;
    public string MenuId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MealId { get; set; } = string.Empty;
    public string MealName { get; set; } = string.Empty;
    public string MealDescription { get; set; } = string.Empty;
    public string MealImage { get; set; } = string.Empty;
    public int DefaultDuration { get; set; }
    public string Date { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
