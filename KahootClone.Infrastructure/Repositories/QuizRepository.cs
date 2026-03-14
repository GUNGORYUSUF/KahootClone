using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Infrastructure.Data;
using MongoDB.Driver;

namespace KahootClone.Infrastructure.Repositories;

public class QuizRepository : IQuizRepository
{
    private readonly MongoDbContext _context;

    // Veritabanı bağlantısı kasanın içine enjekte edilir.
    public QuizRepository(MongoDbContext context)
    {
        _context = context;
    }

    // Oyun verisi MongoDB koleksiyonuna fiziksel olarak kaydedilir.
    public void Add(Quiz quiz)
    {
        _context.Quizzes.InsertOne(quiz);
    }
    // PIN koduna göre eşleşen ilk oyun MongoDB koleksiyonundan bulunarak döndürülür.
    public Quiz? GetByPin(string pin)
    {
        // using kelimesi kaldırılarak doğrudan ilk sonuç talep edilir.
        return _context.Quizzes.Find(q => q.Pin == pin).FirstOrDefault();
    }
    // Oyunun güncel hali (yeni puanlar vb.) veritabanındaki eski veriyle değiştirilir.
    public void Update(Quiz quiz)
    {
        _context.Quizzes.ReplaceOne(q => q.Id == quiz.Id, quiz);
    }
}