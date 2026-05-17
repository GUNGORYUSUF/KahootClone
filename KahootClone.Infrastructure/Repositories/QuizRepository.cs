using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Infrastructure.Data;
using MongoDB.Driver;
using StackExchange.Redis;
using Polly;
using Polly.Retry;
using System.Text.Json;
using System;

namespace KahootClone.Infrastructure.Repositories;

public class QuizRepository : IQuizRepository
{
    private readonly MongoDbContext _context;
    private readonly IDatabase _redisDb;
    private readonly RetryPolicy _retryPolicy;

    // Veritabanı bağlantısı kasanın içine enjekte edilir.
    public QuizRepository(MongoDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redisDb = redis.GetDatabase();

        // YENİ: Polly Zırhı! Ağ kopmalarında veya Timeout durumlarında anında çökmek yerine,
        // işlemi iptal etmeden önce 2 saniye arayla toplam 3 kez yeniden dener.
        _retryPolicy = Policy
            .Handle<Exception>() 
            .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(2));
    }

    // Oyun verisi MongoDB koleksiyonuna fiziksel olarak kaydedilir.
    public void Add(Quiz quiz)
    {
        // Veritabanı işlemi Retry zırhı (Execute) içerisinde çalıştırılır.
        _retryPolicy.Execute(() =>
        {
            _context.Quizzes.InsertOne(quiz);
            
            // YENİ: Performans için Redis Önbelleğine (Cache) ekle (2 saat geçerli)
            _redisDb.StringSet($"quiz_cache:{quiz.Pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2));
        });
    }
    // PIN koduna göre eşleşen ilk oyun MongoDB koleksiyonundan bulunarak döndürülür.
    public Quiz? GetByPin(string pin)
    {
        return _retryPolicy.Execute(() =>
        {
            // YENİ: Önce Redis Önbelleğine (Cache) bakılır
            var cachedData = _redisDb.StringGet($"quiz_cache:{pin}");
            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<Quiz>(cachedData.ToString()!);
            }

            // Önbellekte yoksa MongoDB'den çekilir ve Redis'e yazılır.
            var quiz = _context.Quizzes.Find(q => q.Pin == pin).FirstOrDefault();
            if (quiz != null)
            {
                _redisDb.StringSet($"quiz_cache:{pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2));
            }
            return quiz;
        });
    }
    // Oyunun güncel hali (yeni puanlar vb.) veritabanındaki eski veriyle değiştirilir.
    public void Update(Quiz quiz)
    {
        _retryPolicy.Execute(() =>
        {
            _context.Quizzes.ReplaceOne(q => q.Id == quiz.Id, quiz);
            
            // YENİ: Güncelleme sonrası Redis Önbelleği de senkronize edilir.
            _redisDb.StringSet($"quiz_cache:{quiz.Pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2));
        });
    }

    // YENİ: Yöneticinin kendi oluşturduğu oyunları veritabanından getirir.
    public List<Quiz> GetByCreatorId(string creatorId)
    {
        return _retryPolicy.Execute(() =>
        {
            // YENİ: Soru bankası sadece "Taslak (Draft)" olarak kaydedilen oyunları getirir.
            return _context.Quizzes.Find(q => q.CreatorId == creatorId && q.IsDraft).ToList();
        });
    }

    // YENİ: İstenmeyen oyunu MongoDB ve Redis'ten kalıcı olarak siler.
    public void Delete(string pin)
    {
        _retryPolicy.Execute(() =>
        {
            _context.Quizzes.DeleteOne(q => q.Pin == pin);
            _redisDb.KeyDelete($"quiz_cache:{pin}");
        });
    }
}