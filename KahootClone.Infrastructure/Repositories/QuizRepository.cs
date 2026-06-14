using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Infrastructure.Data;
using MongoDB.Driver;
using StackExchange.Redis;
using Polly;
using Polly.Retry;
using System.Text.Json;
using System;
using MongoDB.Bson;

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

        // TOCTOU (Zamanlama/Çakışma) Koruması: PIN alanına Unique Index basılarak,
        // aynı anda iki sunucunun aynı PIN'i kaydetmesi veritabanı düzeyinde engellenir.
        try
        {
            var indexKeys = Builders<Quiz>.IndexKeys.Ascending(q => q.Pin);
            var indexOptions = new CreateIndexOptions { Unique = true, Sparse = true };
            _context.Quizzes.Indexes.CreateOne(new CreateIndexModel<Quiz>(indexKeys, indexOptions));
        }
        catch (MongoCommandException) { /* Uygulamanın kalkış anında eski çakışan PIN'ler yüzünden çökmesini engeller */ }
    }

    // Oyun verisi MongoDB koleksiyonuna fiziksel olarak kaydedilir.
    public void Add(Quiz quiz)
    {
        // Veritabanı işlemi Retry zırhı (Execute) içerisinde çalıştırılır.
        _retryPolicy.Execute(() =>
        {
            _context.Quizzes.InsertOne(quiz);
            
            try 
            { 
                _redisDb.StringSet($"quiz_cache:{quiz.Pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2)); 
            } 
            catch { /* Redis çökerse oyunu bozma (Fail-Soft) */ }
        });
    }
    // PIN koduna göre eşleşen ilk oyun MongoDB koleksiyonundan bulunarak döndürülür.
    public Quiz? GetByPin(string pin)
    {
        return _retryPolicy.Execute(() =>
        {
            try 
            {
                var cachedData = _redisDb.StringGet($"quiz_cache:{pin}");
                if (cachedData.HasValue) return JsonSerializer.Deserialize<Quiz>(cachedData.ToString()!);
            } 
            catch { /* Redis çökerse veritabanına in */ }

            try
            {
                var quiz = _context.Quizzes.Find(q => q.Pin == pin).FirstOrDefault();
                if (quiz != null)
                {
                    try { _redisDb.StringSet($"quiz_cache:{pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2)); } catch { }
                }
                return quiz;
            }
            catch (Exception ex) when (ex is FormatException || ex is BsonException)
            {
                // DÜZELTME: v3.0.0 sürücüsünün fırlattığı BsonException yakalanıp temizlenir (Self-Healing)
                var rawDb = _context.Quizzes.Database.GetCollection<BsonDocument>("Quizzes");
                rawDb.DeleteMany(Builders<BsonDocument>.Filter.Eq("Pin", pin));
                return null;
            }
        });
    }
    // Oyunun güncel hali (yeni puanlar vb.) veritabanındaki eski veriyle değiştirilir.
    public void Update(Quiz quiz)
    {
        _retryPolicy.Execute(() =>
        {
            // DAĞITIK SİSTEM KORUMASI (OCC): Versiyon kontrolü ile Kayıp Güncelleme (Lost Update) önlenir.
            var currentVersion = quiz.Version;
            quiz.Version++; // Yeni versiyon atanır
            
            var result = _context.Quizzes.ReplaceOne(q => q.Id == quiz.Id && q.Version == currentVersion, quiz);
            
            if (result.MatchedCount == 0)
            {
                quiz.Version--; // Hata durumunda (Entity'yi bozmamak için) versiyonu geri al
                throw new InvalidOperationException("Eşzamanlılık Çakışması (Concurrency Conflict): Bu oyun başka bir sunucu/işlem tarafından güncellendi. Lütfen işlemi tekrar deneyin.");
            }

            // YENİ: Güncelleme sonrası Redis Önbelleği de senkronize edilir.
            _redisDb.StringSet($"quiz_cache:{quiz.Pin}", JsonSerializer.Serialize(quiz), TimeSpan.FromHours(2));
        });
    }

    // YENİ: Yöneticinin kendi oluşturduğu oyunları veritabanından getirir.
    public List<Quiz> GetByCreatorId(string creatorId)
    {
        return _retryPolicy.Execute(() =>
        {
            try
            {
                return _context.Quizzes.Find(q => q.CreatorId == creatorId && q.IsDraft).ToList();
            }
            catch (Exception ex) when (ex is FormatException || ex is BsonException)
            {
                // Self-Healing: MongoDB'deki eski ve bozuk formatlı oyunları temizler ve My-Quizzes 500 hatasını engeller
                var rawDb = _context.Quizzes.Database.GetCollection<BsonDocument>("Quizzes");
                rawDb.DeleteMany(Builders<BsonDocument>.Filter.Eq("CreatorId", creatorId));
                return new List<Quiz>();
            }
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