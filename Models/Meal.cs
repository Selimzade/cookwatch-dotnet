using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CookWatch.API.Models;

public class Meal
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("defaultDuration")]
    public int DefaultDuration { get; set; } = 30;

    [BsonElement("timesCooked")]
    public int TimesCooked { get; set; } = 0;

    [BsonElement("lastCookedAt")]
    public DateTime? LastCookedAt { get; set; }

    [BsonElement("image")]
    public string Image { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
