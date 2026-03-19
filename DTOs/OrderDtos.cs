namespace CookWatch.API.DTOs;

public class OrderDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MenuId { get; set; } = string.Empty;
    public string MenuItemId { get; set; } = string.Empty;
    public string MealName { get; set; } = string.Empty;
    public string MealDescription { get; set; } = string.Empty;
    public string MealImage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? RemainingSeconds { get; set; }
    public string Date { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
