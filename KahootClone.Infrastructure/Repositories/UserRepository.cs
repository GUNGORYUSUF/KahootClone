using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;
using System;

namespace KahootClone.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    // Bağlantı havuzu (Connection Pool) performansını korumak için client static yapıldı
    private static MongoClient? _client;
    private static readonly object _clientLock = new object();
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoDatabase _database;

    private static MongoClient GetMongoClient(string connectionString)
    {
        if (_client == null)
        {
            lock (_clientLock)
            {
                if (_client == null)
                {
                    _client = new MongoClient(connectionString);
                }
            }
        }
        return _client;
    }

    public UserRepository(IConfiguration configuration)
    {
        // Docker ortamında veya yerel ortamda bağlantı dizesini çekiyoruz
        var connectionString = configuration["MongoDbSettings:ConnectionString"] ?? "mongodb://localhost:27017";
        
        var client = GetMongoClient(connectionString);
        _database = client.GetDatabase("KahootDb");
        
        _usersCollection = _database.GetCollection<User>("Users");
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _usersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        try
        {
            return await _usersCollection.Find(x => x.GoogleId == googleId).FirstOrDefaultAsync();
        }
        catch (FormatException)
        {
            // YENİ: Self-Healing (Kendi Kendini Onaran) Veritabanı Mantığı
            // Veritabanında eski Guid (Binary) formatından kalma bozuk veri varsa onu BsonDocument seviyesinde sil.
            var rawCollection = _database.GetCollection<BsonDocument>("Users");
            await rawCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("GoogleId", googleId));
            
            // null dönerek Controller'ın yepyeni, temiz bir string ID ile kayıt açmasını sağla
            return null;
        }
    }

    public async Task CreateAsync(User user)
    {
        await _usersCollection.InsertOneAsync(user);
    }

    public async Task UpdateAsync(User user)
    {
        await _usersCollection.ReplaceOneAsync(x => x.Id == user.Id, user);
    }
}