using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CookWatch.API.Models;

public enum OrderStatus
{
    Pending,
    Cooking,
    Completed,
    Cancelled
}

public class Order
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("menuId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string MenuId { get; set; } = string.Empty;

    [BsonElement("menuItemId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string MenuItemId { get; set; } = string.Empty;

    [BsonElement("mealName")]
    public string MealName { get; set; } = string.Empty;

    [BsonElement("mealDescription")]
    public string MealDescription { get; set; } = string.Empty;

    [BsonElement("mealImage")]
    public string MealImage { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [BsonElement("duration")]
    public int Duration { get; set; } = 30;

    [BsonElement("startTime")]
    public DateTime? StartTime { get; set; }

    [BsonElement("endTime")]
    public DateTime? EndTime { get; set; }

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public double? RemainingSeconds
    {
        get
        {
            if (Status != OrderStatus.Cooking || EndTime == null)
                return null;

            var remaining = (EndTime.Value - DateTime.UtcNow).TotalSeconds;
            return remaining > 0 ? remaining : 0;
        }
    }
}
