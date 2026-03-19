using System.ComponentModel.DataAnnotations;

namespace CookWatch.API.DTOs;

public class PublicUserDto
{
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ShareId { get; set; } = string.Empty;
}

public class PublicViewResponse
{
    public PublicUserDto User { get; set; } = new();
    public string Date { get; set; } = string.Empty;
    public List<MenuDto> Menus { get; set; } = new();
    public DateTime ServerTime { get; set; }
}

public class PublicMenuResponse
{
    public MenuDto Menu { get; set; } = new();
    public List<MenuItemDto> Items { get; set; } = new();
    public List<OrderDto> Orders { get; set; } = new();
}

public class PlaceOrderRequest
{
    [Required]
    public string MenuItemId { get; set; } = string.Empty;
}
