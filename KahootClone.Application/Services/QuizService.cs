using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KahootClone.Application.Services;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;
    private readonly IGameStateRepository _gameStateRepository;

    // Kasa arayüzü (Repository) sisteme enjekte edilir.
    public QuizService(IQuizRepository quizRepository, IGameStateRepository gameStateRepository)
    {
        _quizRepository = quizRepository;
        _gameStateRepository = gameStateRepository;
    }
    public string CreateQuiz(Quiz quiz)
    {
        // PIN kodunun benzersiz olmasını sağlamak için bir döngü eklenebilir, ancak mevcut olasılıkla çakışma riski çok düşüktür.
        // AŞAMA 1: Tahmin edilebilir Random yerine kriptografik olarak güvenli PIN üretici (100000 ile 999999 arası)
        string pin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        
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

    // YENİ: AŞAMA 4 - Oyunun otomatik akışı (Zamanlayıcı) başlatılır.
    public void StartGameFlow(string pin)
    {
        var quizLock = _gameStateRepository.GetQuizLock(pin);
        lock (quizLock)
        {
            var quiz = _quizRepository.GetByPin(pin);
            if (quiz != null && quiz.Questions.Count > 0)
            {
                _gameStateRepository.SetGameState(pin, new GameStateTracker
                {
                    Phase = GamePhase.Question,
                    CurrentQuestionIndex = 0,
                    TimeRemaining = quiz.Questions[0].TimeLimitInSeconds
                });
                quiz.CurrentQuestionStartTime = DateTime.UtcNow;
                _quizRepository.Update(quiz);
            }
        }
    }

    public void StopGameFlow(string pin)
    {
        _gameStateRepository.RemoveGameState(pin);
    }

    public List<GameTickEvent> ProcessTicks()
    {
        var events = new List<GameTickEvent>();
        foreach (var kvp in _gameStateRepository.GetAllActiveGames())
        {
            var pin = kvp.Key;
            var state = kvp.Value;
            var quizLock = _gameStateRepository.GetQuizLock(pin);
            
            lock (quizLock)
            {
                state.TimeRemaining--;
                
                if (state.Phase == GamePhase.Question)
                {
                    ProcessQuestionPhase(pin, state, events);
                }
                else if (state.Phase == GamePhase.Transition)
                {
                    ProcessTransitionPhase(pin, state, events);
                }
            }
        }
        return events;
    }

    private void ProcessQuestionPhase(string pin, GameStateTracker state, List<GameTickEvent> events)
    {
        if (state.TimeRemaining <= 0)
        {
            state.Phase = GamePhase.Transition;
            state.TimeRemaining = 7; // 7 saniye bekleme/geçiş süresi (2 sn cevap, 5 sn tablo)

            // Süre bitince doğru cevabın ID'sini de pakete ekle
            var quiz = _quizRepository.GetByPin(pin);
            var endedQuestion = quiz?.Questions[state.CurrentQuestionIndex];
            var correctOptionId = endedQuestion?.Options.FirstOrDefault(o => o.IsCorrect)?.Id;
            var top5Players = quiz?.Players.OrderByDescending(p => p.Score).Take(5).ToList();
            var waitPayload = new {
                WaitTime = 7,  // 7 saniye (2 saniye cevap gosterimi, 5 saniye tablo gosterimi)
                CorrectOptionId = correctOptionId,
                Leaderboard = top5Players,
                AllAnswered = state.AllAnswered
            };
            events.Add(new GameTickEvent { Pin = pin, EventName = "WaitPhase", Payload = waitPayload });
            state.AllAnswered = false; // Bayrağı sıfırla
        }
        else
        {
            events.Add(new GameTickEvent { Pin = pin, EventName = "TimeUpdate", Payload = state.TimeRemaining });
        }
    }

    private void ProcessTransitionPhase(string pin, GameStateTracker state, List<GameTickEvent> events)
    {
        if (state.TimeRemaining <= 0)
        {
            state.CurrentQuestionIndex++;
            var quiz = _quizRepository.GetByPin(pin);
            if (quiz != null && quiz.Questions.Count > state.CurrentQuestionIndex)
            {
                state.Phase = GamePhase.Question;
                var nextQ = quiz.Questions[state.CurrentQuestionIndex];
                state.TimeRemaining = nextQ.TimeLimitInSeconds;
                quiz.CurrentQuestionStartTime = DateTime.UtcNow;
                _quizRepository.Update(quiz);

                var payload = new {
                    Id = nextQ.Id, Text = nextQ.Text, TimeLimit = nextQ.TimeLimitInSeconds,
                    Options = nextQ.Options.Select(o => new { o.Id, o.Text }).ToList(),
                    CurrentIndex = state.CurrentQuestionIndex + 1, TotalQuestions = quiz.Questions.Count,
                    TotalPlayers = quiz.Players.Count(p => !string.IsNullOrEmpty(p.ConnectionId))
                };
                events.Add(new GameTickEvent { Pin = pin, EventName = "ReceiveQuestion", Payload = payload });
            }
            else
            {
                state.Phase = GamePhase.Ended;
            _gameStateRepository.RemoveGameState(pin);
                events.Add(new GameTickEvent { Pin = pin, EventName = "GameEnded", Payload = quiz?.Players.OrderByDescending(p => p.Score).ToList() });
            }
        }
        else
        {
            events.Add(new GameTickEvent { Pin = pin, EventName = "WaitTimeUpdate", Payload = state.TimeRemaining });
        }
    }

    // YENİ: Oyuncu oyuna katıldığında veya tekrar bağlandığında çalışır.
    public (Player? player, string? errorMessage) JoinOrRejoin(string pin, string nickname, string connectionId)
    {
        var quizLock = _gameStateRepository.GetQuizLock(pin);
        lock (quizLock)
        {
            var quiz = _quizRepository.GetByPin(pin);
            if (quiz == null || !quiz.IsActive) return (null, "Geçersiz PIN veya oyun aktif değil.");

            var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
            
            if (player == null)
            {
                // Yeni oyuncu kaydı
                player = new Player { Id = Guid.NewGuid(), Nickname = nickname, Score = 0, ConnectionId = connectionId };
                quiz.Players.Add(player);
            _gameStateRepository.AddConnection(connectionId, pin, nickname);
                _quizRepository.Update(quiz);
                return (player, null);
            }
            else
            {
                // Takma ad zaten mevcut. Oyuncunun aktif olup olmadığını kontrol et.
                if (!string.IsNullOrEmpty(player.ConnectionId))
                {
                    // Oyuncu zaten aktif (bağlı). Yeni girişi reddet.
                    return (null, "Bu takma ad zaten kullanılıyor.");
                }
                else
                {
                    // Oyuncu daha önce bağlanmış ama kopmuş. Yeniden bağlanmasına izin ver.
                    player.ConnectionId = connectionId;
                _gameStateRepository.AddConnection(connectionId, pin, nickname);
                    _quizRepository.Update(quiz);
                    return (player, null);
                }
            }
        }
    }

    public (string? Pin, string? Nickname) UnregisterPlayer(string connectionId)
    {
        var info = _gameStateRepository.GetConnection(connectionId);
        if (info != null)
        {
            _gameStateRepository.RemoveConnection(connectionId);
            var (pin, nickname) = info.Value;
            var quizLock = _gameStateRepository.GetQuizLock(pin);
            lock (quizLock)
            {
                var quiz = _quizRepository.GetByPin(pin);
                if (quiz != null)
                {
                    var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
                    if (player != null)
                    {
                        player.ConnectionId = string.Empty; // Oyuncuyu pasif olarak işaretle
                        _quizRepository.Update(quiz);
                        return (pin, nickname);
                    }
                }
            }
        }
        return (null, null);
    }

    public void AbandonQuiz(string pin)
    {
        var quizLock = _gameStateRepository.GetQuizLock(pin);
        lock (quizLock)
        {
            var quiz = _quizRepository.GetByPin(pin);
            if (quiz != null)
            {
                // Mark quiz as inactive in DB
                quiz.IsActive = false;
                _quizRepository.Update(quiz);

                // Remove players from the connection map
                foreach (var connectionId in quiz.Players.Select(p => p.ConnectionId).Where(id => !string.IsNullOrEmpty(id)))
                {
                    _gameStateRepository.RemoveConnection(connectionId);
                }
            }
        }
        // Clean up in-memory state
        _gameStateRepository.RemoveQuizLock(pin);
    }

    public object? GetFullGameState(string pin)
    {
        var quiz = _quizRepository.GetByPin(pin);
        if (quiz == null) return null;

        var gameState = _gameStateRepository.GetGameState(pin);

        Question? currentQuestion = null;
        if (gameState != null && quiz.Questions.Count > gameState.CurrentQuestionIndex)
        {
            currentQuestion = quiz.Questions[gameState.CurrentQuestionIndex];
        }

        // İstemciye gönderilecek güvenli soru paketi (doğru cevap bilgisi olmadan)
        object? secureCurrentQuestion = null;
        if (currentQuestion != null)
        {
            secureCurrentQuestion = new {
                Id = currentQuestion.Id,
                Text = currentQuestion.Text,
                TimeLimit = currentQuestion.TimeLimitInSeconds,
                Options = currentQuestion.Options.Select(o => new { o.Id, o.Text }).ToList()
            };
        }

        int answeredCount = 0;
        int totalActiveCount = quiz.Players.Count(p => !string.IsNullOrEmpty(p.ConnectionId));
        if (currentQuestion != null)
        {
            answeredCount = quiz.Players.Count(p => p.AnsweredQuestionIds.Contains(currentQuestion.Id) && !string.IsNullOrEmpty(p.ConnectionId));
        }

        return new
        {
            Quiz = new {
                Pin = quiz.Pin,
                Players = quiz.Players,
                QuestionsCount = quiz.Questions.Count
            },
            GameState = gameState,
            CurrentQuestion = secureCurrentQuestion,
            AnsweredCount = answeredCount,
            TotalActiveCount = totalActiveCount
        };
    }

    // Sistemin test edilebilmesi için varsayılan örnek sorular üretilir.
    private static List<Question> GenerateSampleQuestions()
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
    // Oyuncunun verdiği cevap kontrol edilir ve puanı hesaplanır.
    public (bool IsCorrect, int AnsweredCount, int TotalCount, int PointsEarned) SubmitAnswer(string pin, string nickname, Guid questionId, Guid optionId)
    {
        var quizLock = _gameStateRepository.GetQuizLock(pin);
        lock (quizLock)
        {
            var quiz = _quizRepository.GetByPin(pin);
            if (quiz == null) return (false, 0, 0, 0);

            var question = quiz.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question == null) return (false, 0, 0, 0);
            var option = question.Options.FirstOrDefault(o => o.Id == optionId);
            
            bool isCorrect = option != null && option.IsCorrect;

            // AŞAMA 4: Hile Koruması - Süre bittiyse (veya geçiş aşamasındaysa) gönderilen cevaplar kesinlikle reddedilir.
            var state = _gameStateRepository.GetGameState(pin);
            if (state != null && state.Phase != GamePhase.Question)
                return (false, 0, 0, 0);

            // Oyuncu listesi kontrol edilir. Eğer oyuncu oyunda kayıtlı değilse, bu geçersiz bir istektir.
            var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
            if (player == null)
            {
                // Oyuncu lobide kayıtlı değilse cevap gönderemez. Güvenlik için isteği sessizce yoksay.
                return (false, 0, 0, 0);
            }

            // AŞAMA 1: Çift cevap kontrolü (Oyuncu bu soruyu daha önce cevapladıysa işlemi yoksay)
            if (player.AnsweredQuestionIds.Contains(questionId))
            {
                var apCount = quiz.Players.Count(p => !string.IsNullOrEmpty(p.ConnectionId));
                var ansCount = quiz.Players.Count(p => p.AnsweredQuestionIds.Contains(questionId) && !string.IsNullOrEmpty(p.ConnectionId));
                return (false, ansCount, apCount, 0);
            }

            // Oyuncunun bu soruya cevap verdiğini listesine ekleyelim
            player.AnsweredQuestionIds.Add(questionId);

            int points = 0;
            // Cevap doğruysa hıza dayalı dinamik puanlama yapılır.
            if (isCorrect)
            {
                points = CalculatePoints(quiz.CurrentQuestionStartTime, question.TimeLimitInSeconds);
                player.Score += points;
            }

            // Oyuncunun kazandığı yeni puanlar veritabanına kalıcı olarak kaydedilir.
            _quizRepository.Update(quiz);

            // Güncel sayıları hesapla
            var activePlayers = quiz.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)).ToList();
            int activePlayerCount = activePlayers.Count;
            int answeredCount = activePlayers.Count(p => p.AnsweredQuestionIds.Contains(questionId));

            // YENİ: Bütün aktif oyuncular cevap verdi mi kontrol et.
            UpdateGameStateIfAllAnswered(state, activePlayerCount, answeredCount);

            return (isCorrect, answeredCount, activePlayerCount, points);
        }
    }

    private static int CalculatePoints(DateTime startTime, double timeLimitInSeconds)
    {
        var timeTaken = (DateTime.UtcNow - startTime).TotalSeconds;
        
        if (timeTaken < 0) timeTaken = 0;
        if (timeTaken > timeLimitInSeconds) timeTaken = timeLimitInSeconds;

        // Puanlama Algoritması: Hızlı cevap veren daha yüksek puan alır (Maks: 1000, Min: 500)
        double scoreFactor = 1.0 - (timeTaken / (timeLimitInSeconds * 2.0));
        return (int)Math.Round(1000 * scoreFactor);
    }

    private static void UpdateGameStateIfAllAnswered(GameStateTracker? state, int activePlayerCount, int answeredCount)
    {
        // Eğer herkes cevap verdiyse süreyi hemen bitir (Transition fazına geçişi tetikle).
        if (state != null && state.Phase == GamePhase.Question && activePlayerCount > 0 && answeredCount >= activePlayerCount)
        {
            state.TimeRemaining = 0;
            state.AllAnswered = true;
        }
    }
}