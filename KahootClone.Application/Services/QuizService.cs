using KahootClone.Domain.Entities;
using KahootClone.Application.Interfaces;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using KahootClone.Application.Constants;

namespace KahootClone.Application.Services;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<QuizService> _logger;
    private readonly TimeProvider _timeProvider;

    // Çöp Toplayıcı (GC) Yükünü Hafifletmek İçin Statik Önbellek (Sıfır Tahsisat)
    private static readonly object[] _timePayloads = Enumerable.Range(0, 3601).Select(i => (object)i).ToArray();
    
    // Yazma Fırtınası (Write Storm) Çözümü İçin Toplu Yazma Önbelleği (Write-Behind Cache)
    private static readonly ConcurrentDictionary<string, Quiz> _writeBehindCache = new();

    // Kasa arayüzü (Repository) sisteme enjekte edilir.
    public QuizService(IQuizRepository quizRepository, IGameStateRepository gameStateRepository, IMessagePublisher messagePublisher, ILogger<QuizService> logger, TimeProvider timeProvider)
    {
        _quizRepository = quizRepository;
        _gameStateRepository = gameStateRepository;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _timeProvider = timeProvider;
    }
    public string CreateQuiz(Quiz quiz)
    {
        string pin;
        int retryCount = 0;

        // YENİ: MongoDB'de _id çakışmasını (Duplicate Key Error) önlemek için benzersiz kimlik atanır.
        if (quiz.Id == Guid.Empty)
        {
            quiz.Id = Guid.NewGuid();
        }

        // PIN kodunun benzersiz olmasını sağlamak için (Unique Check) veritabanı kontrolü yapılıyor.
        do
        {
            // Kaynak Tüketimi (DoS) Koruması: Sonsuz döngü engellendi.
            if (retryCount++ > 100) throw new InvalidOperationException("Sistemde uygun PIN kodu kalmadı veya çakışma çok yüksek.");
            pin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        } while (_quizRepository.GetByPin(pin) != null);
        
        // DDD Kapsülleme Entegrasyonu: Gelen oyun bir "Taslak" (Soru Bankası Kaydı) ise aktif etme!
        if (quiz.IsDraft)
        {
            quiz.SetAsDraft(pin);
        }
        else
        {
            quiz.Activate(pin);
        }

        // Dışarıdan soru gelmişse onları kullan, gelmemişse örnek soruları yükle (Geriye dönük uyumluluk)
        if (quiz.Questions != null && quiz.Questions.Count > 0)
        {
            foreach (var q in quiz.Questions)
            {
                if (q.Id == Guid.Empty) q.Id = Guid.NewGuid();
                foreach (var o in q.Options)
                {
                    if (o.Id == Guid.Empty) o.Id = Guid.NewGuid();
                }
            }
        }
        else
        {
            quiz.Questions = GenerateSampleQuestions();
        }

        // Oluşturulan oyun bilgileri ve içindeki tüm sorular veritabanına kalıcı olarak kaydedilir.
        _quizRepository.Add(quiz);

        return pin;
    }
    // İstek yapıldığında, kasa üzerinden PIN koduna ait oyun bilgisi çekilir.
    public Quiz? GetQuizByPin(string pin)
    {
        if (_writeBehindCache.TryGetValue(pin, out var cachedQuiz)) return cachedQuiz;
        return _quizRepository.GetByPin(pin);
    }

    // YENİ: Yöneticinin kendi oluşturduğu oyunları getirir
    public List<Quiz> GetQuizzesByCreatorId(string creatorId)
    {
        return _quizRepository.GetByCreatorId(creatorId);
    }

    // YENİ: AŞAMA 4 - Oyunun otomatik akışı (Zamanlayıcı) başlatılır.
    public async Task StartGameFlowAsync(string pin)
    {
        // Dağıtık (Redis) kilit mekanizması kullanılır. Blok bitince kilit salınır.
        await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
        {
            var quiz = GetQuizByPin(pin);
            if (quiz != null && quiz.Questions.Count > 0)
            {
                // MİMARİ DÜZELTME: Oyun başlar başlamaz soruyu göndermek yerine 3 saniyelik Transition (GetReady) fazı ile başlatılır.
                _gameStateRepository.SetGameState(pin, new GameStateTracker
                {
                    Phase = GamePhase.Transition,
                    CurrentQuestionIndex = -1, // İlk soruya (0) geçiş yapması için -1 ayarlanır.
                    TimeRemaining = 4 // 4 saniyelik 3-2-1-Başla! sayacı
                });
                quiz.MarkQuestionStartTime(_timeProvider.GetUtcNow().UtcDateTime); // Çift tıklama (Idempotency) koruması için geçici olarak işaretle
                _writeBehindCache[quiz.Pin] = quiz;
            }
        }
    }

    public void StopGameFlow(string pin)
    {
        _gameStateRepository.RemoveGameState(pin);
    }

    public async Task<List<GameTickEvent>> ProcessTicksAsync()
    {
        var events = new ConcurrentBag<GameTickEvent>();
        try
        {
            var activeGames = _gameStateRepository.GetAllActiveGames().ToList();
            var tasks = activeGames.Select(async kvp =>
            {
                var pin = kvp.Key;
                try
                {
                    // DAĞITIK SİSTEM KORUMASI: Bu saniye (Tick) için kilit alınamazsa, bu oyunu şu an başka bir sunucu işliyor demektir.
                    if (!await _gameStateRepository.TryAcquireTickLockAsync(pin)) return;

                    await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
                    {
                        // GİZLİ YARIŞ DURUMU (Stale State Modification) ÇÖZÜMÜ: State, kilit alındıktan sonra Redis'ten taze olarak okunur.
                        var state = _gameStateRepository.GetGameState(pin);
                        if (state == null) return;

                        // YENİ VİZYON: Nesne değiştirilemez (Immutable)! 'with' ile yeni bir kopya üretilir.
                        var newState = state with { TimeRemaining = state.TimeRemaining - 1 };
                        
                        // OCP İhlali Çözümü: if/else yerine, yeni fazların (Örn: Podyum) kolayca eklenebileceği switch yapısına geçildi.
                        switch (newState.Phase)
                        {
                            case GamePhase.Question:
                                newState = ProcessQuestionPhase(pin, newState, events);
                                break;
                            case GamePhase.Transition:
                                newState = ProcessTransitionPhase(pin, newState, events);
                                break;
                        }

                        // AŞAMA 6 DÜZELTME: Bellek içi referans yerine Redis'ten JSON kopya aldığımız için,
                        // değişen süreyi ve durumu Redis'e geri kaydetmemiz gerekiyor. (Oyun bitmediyse)
                        if (newState.Phase != GamePhase.Ended)
                        {
                            _gameStateRepository.SetGameState(pin, newState);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HATA] Oyun dongusu (Tick) islenirken {Pin} numarali oyunda hata olustu.", pin);
                }
            });
            await Task.WhenAll(tasks);

            // YAZMA FIRTINASI (Write Storm) ÇÖZÜMÜ: Toplu Yazma (Batching) İşlemi
            // Soru cevaplama anında MongoDB'yi kilitlenmekten korumak için bellekte biriken
            // güncellemeler, saniyede 1 kez tek seferde (Bulk) veritabanına aktarılır.
            foreach (var cachePin in _writeBehindCache.Keys)
            {
                if (_writeBehindCache.TryRemove(cachePin, out var cachedQuiz))
                {
                    try 
                    { 
                        // ÇAKIŞMA (Race Condition) ÇÖZÜMÜ: Toplu yazma işlemi de Redis kilidi içine alındı.
                        await using (await _gameStateRepository.AcquireQuizLockAsync(cachePin))
                        {
                            _quizRepository.Update(cachedQuiz); 
                        }
                    } 
                    catch (Exception ex) { _logger.LogWarning(ex, "Batch update sirasinda OCC veya DB hatasi, yoksayildi."); }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HATA] ProcessTicks ana dongusunde beklenmeyen kritik hata olustu.");
        }
        return events.ToList();
    }

    private GameStateTracker ProcessQuestionPhase(string pin, GameStateTracker state, ConcurrentBag<GameTickEvent> events)
    {
        if (state.TimeRemaining <= 0)
        {
            var newState = state with { 
                Phase = GamePhase.Transition, 
                TimeRemaining = 7, // 7 saniye bekleme/geçiş süresi (2 sn cevap, 5 sn tablo)
                AllAnswered = false // Bayrağı sıfırla
            };

            // Süre bitince doğru cevabın ID'sini de pakete ekle
            var quiz = GetQuizByPin(pin);
            var endedQuestion = quiz?.Questions[state.CurrentQuestionIndex];
            var correctOptionId = endedQuestion?.Options.FirstOrDefault(o => o.IsCorrect)?.Id;
            var top5Players = quiz?.Players.OrderByDescending(p => p.Score).Take(5).Select(p => new { p.Nickname, p.Score, p.AvatarUrl }).ToList();
            var waitPayload = new {
                WaitTime = 7,  // 7 saniye (2 saniye cevap gosterimi, 5 saniye tablo gosterimi)
                CorrectOptionId = correctOptionId,
                Leaderboard = top5Players,
                AllAnswered = state.AllAnswered
            };
            events.Add(new GameTickEvent { Pin = pin, EventName = SignalREvents.WaitPhase, Payload = waitPayload });
            return newState;
        }
        else
        {
            object timePayload = state.TimeRemaining >= 0 && state.TimeRemaining <= 3600 ? _timePayloads[(int)state.TimeRemaining] : state.TimeRemaining;
            events.Add(new GameTickEvent { Pin = pin, EventName = SignalREvents.TimeUpdate, Payload = timePayload });
            return state;
        }
    }

    private GameStateTracker ProcessTransitionPhase(string pin, GameStateTracker state, ConcurrentBag<GameTickEvent> events)
    {
        if (state.TimeRemaining <= 0)
        {
            var nextIndex = state.CurrentQuestionIndex + 1;
            var quiz = GetQuizByPin(pin);
            if (quiz != null && quiz.Questions.Count > nextIndex)
            {
                var nextQ = quiz.Questions[nextIndex];
                var newState = state with { 
                    Phase = GamePhase.Question, 
                    CurrentQuestionIndex = nextIndex,
                    TimeRemaining = nextQ.TimeLimitInSeconds
                };
                quiz.MarkQuestionStartTime(_timeProvider.GetUtcNow().UtcDateTime);
                _writeBehindCache[quiz.Pin] = quiz;

                var payload = new {
                    Id = nextQ.Id, Text = nextQ.Text, TimeLimit = nextQ.TimeLimitInSeconds,
                    Options = nextQ.Options.Select(o => new { o.Id, o.Text }).ToList(),
                    CurrentIndex = nextIndex + 1, TotalQuestions = quiz.Questions.Count,
                    TotalPlayers = quiz.Players.Count(p => !string.IsNullOrEmpty(p.ConnectionId))
                };
                events.Add(new GameTickEvent { Pin = pin, EventName = SignalREvents.ReceiveQuestion, Payload = payload });
                return newState;
            }
            else
            {
                var newState = state with { Phase = GamePhase.Ended };
            _gameStateRepository.RemoveGameState(pin);
                var safeLeaderboard = quiz?.Players.OrderByDescending(p => p.Score).Select(p => new { p.Nickname, p.Score, p.AvatarUrl }).ToList();
                events.Add(new GameTickEvent { Pin = pin, EventName = SignalREvents.GameEnded, Payload = safeLeaderboard });
            
            // YENİ: Oyun bittiğinde veritabanı kayıt yükü ana akıştan koparılıp RabbitMQ kuyruğuna fırlatılır!
            if (quiz != null)
            {
                _messagePublisher.Publish("game_ended_queue", quiz);
            }
                return newState;
            }
        }
        else
        {
            object timePayload = state.TimeRemaining >= 0 && state.TimeRemaining <= 3600 ? _timePayloads[(int)state.TimeRemaining] : state.TimeRemaining;
            events.Add(new GameTickEvent { Pin = pin, EventName = SignalREvents.WaitTimeUpdate, Payload = timePayload });
            return state;
        }
    }

    // YENİ: Oyuncu oyuna katıldığında veya tekrar bağlandığında çalışır.
    public async Task<(Player? player, string? errorMessage, string? sessionToken)> JoinOrRejoinAsync(string pin, string nickname, string connectionId, string? sessionToken = null, string? googleToken = null, string? avatarUrl = null)
    {
        // Güvenlik Koruması (Sınırsız Girdi / BSON Sınırı): Max metin uzunlukları kontrol edilir.
        if (!string.IsNullOrEmpty(nickname) && nickname.Length > 30) return (null, "Takma ad en fazla 30 karakter olabilir.", null);
        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.Length > 2000) return (null, "Avatar URL çok uzun.", null);

        await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
        {
            var quiz = GetQuizByPin(pin);
            if (quiz == null || !quiz.IsActive) return (null, "Geçersiz PIN veya oyun aktif değil.", null);

            // YENİ: Google Auth zorunluluğu kontrolü
            if (quiz.RequireGoogleAuth && string.IsNullOrEmpty(googleToken))
            {
                // Eğer oyun Google istiyorsa ama token gelmemişse (veya doğrudan nick ile girmeye çalışıyorsa) reddet!
                return (null, "Bu oyuna sadece Google ile giriş yapanlar katılabilir!", null);
            }

            var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
            
            if (player == null)
            {
                // DDD Entegrasyonu: Yeni oyuncu kaydı
                player = new Player(Guid.NewGuid(), nickname, connectionId, avatarUrl);
                quiz.AddPlayer(player);
            _gameStateRepository.AddConnection(connectionId, pin, nickname);
                _writeBehindCache[quiz.Pin] = quiz;
                
                // Oturum Güvenliği (Session Hijacking): Sadece GUID değil, HMAC ile imzalanmış güvenli token dönülür.
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("KahootClone_Secure_Session_Key_123!"));
                var hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(player.Id.ToString())));
                return (player, null, $"{player.Id}:{hash}");
            }
            else
            {
                // Güvenlik Kontrolü (Session Hijacking): Gelen token HMAC ile doğrulanır.
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("KahootClone_Secure_Session_Key_123!"));
                var expectedHash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(player.Id.ToString())));
                var expectedToken = $"{player.Id}:{expectedHash}";

                if (sessionToken != expectedToken)
                {
                    return (null, "Bu takma ad şu anda başka bir oyuncu tarafından kullanılıyor.", null);
                }
                else
                {
                    // DDD Entegrasyonu: Oyuncu daha önce bağlanmış ama kopmuş. Yeniden bağlanmasına izin ver.
                    player.UpdateConnection(connectionId);
                    if (!string.IsNullOrEmpty(avatarUrl)) player.UpdateAvatar(avatarUrl);
                _gameStateRepository.AddConnection(connectionId, pin, nickname);
                    _writeBehindCache[quiz.Pin] = quiz;
                return (player, null, expectedToken);
                }
            }
        }
    }

    public async Task<(string? Pin, string? Nickname)> UnregisterPlayerAsync(string connectionId)
    {
        var info = _gameStateRepository.GetConnection(connectionId);
        if (info != null)
        {
            _gameStateRepository.RemoveConnection(connectionId);
            var (pin, nickname) = info.Value;
            await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
            {
                var quiz = GetQuizByPin(pin);
                if (quiz != null)
                {
                    var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
                    if (player != null)
                    {
                        player.Disconnect(); // DDD Entegrasyonu: Oyuncuyu pasif olarak işaretle
                        _writeBehindCache[quiz.Pin] = quiz;
                        return (pin, nickname);
                    }
                }
            }
        }
        return (null, null);
    }

    public async Task AbandonQuizAsync(string pin)
    {
        await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
        {
            var quiz = GetQuizByPin(pin);
            if (quiz != null)
            {
                // Mark quiz as inactive in DB
                quiz.Deactivate(); // DDD Entegrasyonu
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
        
        // YENİ DÜZELTME: Oyun terk edildiğinde aktif oyunlar listesinden (Redis) tamamen çıkarıyoruz.
        _gameStateRepository.RemoveGameState(pin);
        
        // YENİ DÜZELTME: Oyun terk edildiğinde bellekte asılı kalmış güncellemeleri çöpe at.
        _writeBehindCache.TryRemove(pin, out _);
    }

    public object? GetFullGameState(string pin)
    {
        var quiz = GetQuizByPin(pin);
        if (quiz == null) return null;

        var gameState = _gameStateRepository.GetGameState(pin);

        Question? currentQuestion = null;
        // YENİ DÜZELTME: Oyun GetReady (3-2-1) aşamasındayken indeks -1 olacağı için veritabanında -1. soruyu aramayı engelliyoruz.
        if (gameState != null && gameState.CurrentQuestionIndex >= 0 && quiz.Questions.Count > gameState.CurrentQuestionIndex)
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
                // KİMLİK SIZINTISI (Information Exposure) KAPATILDI: ConnectionId ve Id (Oturum token'ı) gizlendi.
                Players = quiz.Players.Select(p => new { p.Nickname, p.Score, p.AvatarUrl, IsActive = !string.IsNullOrEmpty(p.ConnectionId) }).ToList(),
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
    public async Task<(bool IsCorrect, int AnsweredCount, int TotalCount, int PointsEarned)> SubmitAnswerAsync(string pin, string nickname, Guid questionId, Guid optionId)
    {
        await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
        {
            var quiz = GetQuizByPin(pin);
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

            // DDD Entegrasyonu: Oyuncunun bu soruya cevap verdiğini listesine ekleyelim
            player.MarkQuestionAsAnswered(questionId);

            int points = 0;
            // Cevap doğruysa hıza dayalı dinamik puanlama yapılır.
            if (isCorrect)
            {
                // SAAT KAYMASI (Clock Skew) ÇÖZÜMÜ: Mutlak sunucu saati yerine göreceli Tick sayacı kullanılarak adaletsizlik önlenir.
                points = CalculatePoints(state?.TimeRemaining ?? 0, question.TimeLimitInSeconds);
                player.AddScore(points);
            }

            // Oyuncunun kazandığı yeni puanlar veritabanına kalıcı olarak kaydedilir.
            // YAZMA FIRTINASI (Write Storm) ÇÖZÜMÜ: Anlık MongoDB yazması (Update) iptal edildi. Puanlar belleğe alınır ve ProcessTicksAsync içinde toplu yazılır.
            _writeBehindCache[quiz.Pin] = quiz;

            // Güncel sayıları hesapla
            var activePlayers = quiz.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)).ToList();
            int activePlayerCount = activePlayers.Count;
            int answeredCount = activePlayers.Count(p => p.AnsweredQuestionIds.Contains(questionId));

            // YENİ: Bütün aktif oyuncular cevap verdi mi kontrol et.
            var newState = UpdateGameStateIfAllAnswered(state, activePlayerCount, answeredCount);

            // AŞAMA 6 DÜZELTME: Bütün oyuncular cevap verdiyse (süreyi 0 yaptıysa) bunu Redis'e yansıt.
            if (newState != null)
            {
                _gameStateRepository.SetGameState(pin, newState);
            }

            return (isCorrect, answeredCount, activePlayerCount, points);
        }
    }

    private static int CalculatePoints(double timeRemaining, double timeLimitInSeconds)
    {
        double timeTaken = timeLimitInSeconds - timeRemaining;
        
        if (timeTaken < 0) timeTaken = 0;
        if (timeTaken > timeLimitInSeconds) timeTaken = timeLimitInSeconds;

        // Puanlama Algoritması: Hızlı cevap veren daha yüksek puan alır (Maks: 1000, Min: 500)
        double scoreFactor = 1.0 - (timeTaken / (timeLimitInSeconds * 2.0));
        return (int)Math.Round(1000 * scoreFactor);
    }

    private static GameStateTracker? UpdateGameStateIfAllAnswered(GameStateTracker? state, int activePlayerCount, int answeredCount)
    {
        // Eğer herkes cevap verdiyse süreyi hemen bitir (Transition fazına geçişi tetikle).
        if (state != null && state.Phase == GamePhase.Question && activePlayerCount > 0 && answeredCount >= activePlayerCount)
        {
            return state with { TimeRemaining = 0, AllAnswered = true };
        }
        return state;
    }

    public async Task<string?> KickPlayerAsync(string pin, string nickname)
    {
        await using (await _gameStateRepository.AcquireQuizLockAsync(pin))
        {
            var quiz = GetQuizByPin(pin);
            if (quiz != null)
            {
                var player = quiz.Players.FirstOrDefault(p => p.Nickname == nickname);
                if (player != null)
                {
                    var connId = player.ConnectionId;
                    quiz.RemovePlayer(player); // DDD Entegrasyonu
                    _writeBehindCache[quiz.Pin] = quiz;
                    if (!string.IsNullOrEmpty(connId))
                    {
                        _gameStateRepository.RemoveConnection(connId);
                    }
                    return connId;
                }
            }
        }
        return null;
    }

    public void DeleteQuiz(string pin)
    {
        _quizRepository.Delete(pin);
        _writeBehindCache.TryRemove(pin, out _); // Veritabanından silinen oyunun bellekteki güncellemelerini çöpe at.
    }
}