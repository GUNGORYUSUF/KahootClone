using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;

namespace KahootClone.Application.Services;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;

    // Kasa arayüzü (Repository) sisteme enjekte edilir.
    public QuizService(IQuizRepository quizRepository)
    {
        _quizRepository = quizRepository;
    }

    public string CreateQuiz(Quiz quiz)
    {
        Random random = new Random();
        string pin = random.Next(100000, 999999).ToString();
        
        quiz.Pin = pin;
        quiz.IsActive = true;

        // Oyuna örnek sorular (Seed Data) otomatik olarak eklenir.
        quiz.Questions = GenerateSampleQuestions();

        // Oluşturulan oyun bilgileri ve içindeki tüm sorular veritabanına kalıcı olarak kaydedilir.
        _quizRepository.Add(quiz);

        return pin;
    }
    // İstek yapıldığında, kasa üzerinden PIN koduna ait oyun bilgisi çekilir.
    public Quiz? GetQuizByPin(string pin)
    {
        return _quizRepository.GetByPin(pin);
    }

    // YENİ: Sorunun başlama zamanı kaydedilerek hıza dayalı puanlama için referans oluşturulur.
    public void StartQuestion(string pin)
    {
        var quiz = _quizRepository.GetByPin(pin);
        if (quiz != null)
        {
            quiz.CurrentQuestionStartTime = DateTime.UtcNow;
            _quizRepository.Update(quiz);
        }
    }

    // Sistemin test edilebilmesi için varsayılan örnek sorular üretilir.
    private List<Question> GenerateSampleQuestions()
    {
        return new List<Question>
        {
            new Question
            {
                Id = Guid.NewGuid(),
                Text = "Yapay Zeka terimi ilk kez hangi yıl kullanılmıştır?",
                TimeLimitInSeconds = 20,
                Options = new List<Option>
                {
                    new Option { Id = Guid.NewGuid(), Text = "1945", IsCorrect = false },
                    new Option { Id = Guid.NewGuid(), Text = "1956", IsCorrect = true },
                    new Option { Id = Guid.NewGuid(), Text = "1969", IsCorrect = false },
                    new Option { Id = Guid.NewGuid(), Text = "1984", IsCorrect = false }
                }
            },
            new Question
            {
                Id = Guid.NewGuid(),
                Text = "Yazılım dünyasında 'Bug' (Böcek) teriminin ortaya çıkmasına sebep olan canlı hangi bilgisayarın donanımına sıkışmıştır?",
                TimeLimitInSeconds = 20,
                Options = new List<Option>
                {
                    new Option { Id = Guid.NewGuid(), Text = "ENIAC", IsCorrect = false },
                    new Option { Id = Guid.NewGuid(), Text = "Harvard Mark II", IsCorrect = true },
                    new Option { Id = Guid.NewGuid(), Text = "IBM 704", IsCorrect = false },
                    new Option { Id = Guid.NewGuid(), Text = "Altair 8800", IsCorrect = false }
                }
            }
        };
    }
    // Öğrencinin verdiği cevap kontrol edilir ve puanı hesaplanır.
    public bool SubmitAnswer(string pin, string nickname, Guid questionId, Guid optionId)
    {
        var quiz = _quizRepository.GetByPin(pin);
        if (quiz == null) return false;

        var question = quiz.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question == null) return false;
        var option = question?.Options.FirstOrDefault(o => o.Id == optionId);
        
        bool isCorrect = option != null && option.IsCorrect;

        // Oyuncu listesi kontrol edilir, yoksa listeye dahil edilir.
        var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
        if (player == null)
        {
            player = new Player { Id = Guid.NewGuid(), Nickname = nickname, Score = 0 };
            quiz.Players.Add(player);
        }

        // Cevap doğruysa hıza dayalı dinamik puanlama yapılır.
        if (isCorrect)
        {
            var timeTaken = (DateTime.UtcNow - quiz.CurrentQuestionStartTime).TotalSeconds;
            
            if (timeTaken < 0) timeTaken = 0;
            if (timeTaken > question.TimeLimitInSeconds) timeTaken = question.TimeLimitInSeconds;

            // Puanlama Algoritması: Hızlı cevap veren daha yüksek puan alır (Maks: 1000, Min: 500)
            double scoreFactor = 1.0 - (timeTaken / (question.TimeLimitInSeconds * 2.0));
            int points = (int)Math.Round(1000 * scoreFactor);
            
            player.Score += points;
        }

        // Oyuncunun kazandığı yeni puanlar veritabanına kalıcı olarak kaydedilir.
        _quizRepository.Update(quiz);

        return isCorrect;
    }
}