using MongoDB.Bson;
using MongoDB.Driver;
using CookWatch.API.Models;

namespace CookWatch.API.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public IMongoCollection<User> Users { get; }
    public IMongoCollection<Meal> Meals { get; }
    public IMongoCollection<Menu> Menus { get; }
    public IMongoCollection<MenuItem> MenuItems { get; }
    public IMongoCollection<Order> Orders { get; }
    public IMongoCollection<CookingSession> CookingSessions { get; }

    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_URI")
            ?? configuration["MongoDB:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDB connection string is not configured.");

        var databaseName = configuration["MongoDB:DatabaseName"] ?? "cookwatch";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        Users = _database.GetCollection<User>("users");
        Meals = _database.GetCollection<Meal>("meals");
        Menus = _database.GetCollection<Menu>("menus");
        MenuItems = _database.GetCollection<MenuItem>("menuitems");
        Orders = _database.GetCollection<Order>("orders");
        CookingSessions = _database.GetCollection<CookingSession>("cookingsessions");
    }

    public async Task CreateIndexesAsync()
    {
        // User indexes
        await Users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.ShareId),
                new CreateIndexOptions { Unique = true })
        });

        // Meal indexes
        await Meals.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Meal>(
                Builders<Meal>.IndexKeys.Ascending(m => m.UserId)),
            new CreateIndexModel<Meal>(
                Builders<Meal>.IndexKeys.Descending(m => m.TimesCooked))
        });

        // Menu indexes
        await Menus.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Menu>(
                Builders<Menu>.IndexKeys.Ascending(m => m.UserId)),
            new CreateIndexModel<Menu>(
                Builders<Menu>.IndexKeys.Ascending(m => m.Date))
        });

        // MenuItem indexes
        await MenuItems.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<MenuItem>(
                Builders<MenuItem>.IndexKeys.Ascending(mi => mi.MenuId)),
            new CreateIndexModel<MenuItem>(
                Builders<MenuItem>.IndexKeys.Ascending(mi => mi.UserId))
        });

        // Order indexes
        await Orders.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(o => o.UserId)),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(o => o.MenuId)),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(o => o.Date))
        });

        // CookingSession indexes
        await CookingSessions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CookingSession>(
                Builders<CookingSession>.IndexKeys.Ascending(s => s.UserId)),
            new CreateIndexModel<CookingSession>(
                Builders<CookingSession>.IndexKeys.Ascending(s => s.SessionDate)),
            new CreateIndexModel<CookingSession>(
                Builders<CookingSession>.IndexKeys.Ascending(s => s.MealId))
        });
    }
}
