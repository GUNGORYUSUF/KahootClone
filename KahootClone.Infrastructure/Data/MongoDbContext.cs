using KahootClone.Domain.Entities;
using MongoDB.Driver;

namespace KahootClone.Infrastructure.Data;

public class MongoDbContext
{
    // Veritabanı örneği bellekte tutulur.
    private readonly IMongoDatabase _database;

    // Yapıcı metot (Constructor) üzerinden bağlantı ayarları alınır ve veritabanı bağlantısı gerçekleştirilir.
    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    // Sistemdeki Quiz (Oyun) koleksiyonuna erişim sağlanır.
    public IMongoCollection<Quiz> Quizzes => _database.GetCollection<Quiz>("Quizzes");
    
    // Sistemdeki Player (Oyuncu) koleksiyonuna erişim sağlanır.
    public IMongoCollection<Player> Players => _database.GetCollection<Player>("Players");
}