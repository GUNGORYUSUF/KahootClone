using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using KahootClone.Infrastructure.Data;

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
}