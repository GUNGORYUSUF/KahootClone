using Microsoft.AspNetCore.SignalR;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace KahootClone.Api.Hubs;

public class GameHub : Hub<IGameClient>
{
    private readonly IQuizService _quizService;

    public GameHub(IQuizService quizService)
    {
        _quizService = quizService;
    }

    // YENİ: Yöneticinin (Host) oyuna oyuncu olarak dahil olmadan sadece gruba katılması sağlanır.
    // (Yöneticinin skor tablosunda 0 puanla listelenme hatasını düzeltir)
    [Authorize(Roles = "Host")]
    public async Task JoinAsManager(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        if (quiz == null || !quiz.IsActive)
        {
            await Clients.Caller.Error("Geçersiz PIN veya oyun aktif değil.");
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
    }

    // YENİ: Sayfayı yenileyen yöneticinin oyuna tekrar dahil olmasını ve oyun durumunu almasını sağlar.
    [Authorize(Roles = "Host")]
    public async Task RejoinAsManager(string pin)
    {
        // Önce gruba dahil et ki yayınları alabilsin.
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);

        // Ardından oyunun tam durumunu çek.
        var fullState = _quizService.GetFullGameState(pin);
        if (fullState != null)
        {
            // Durum bilgisini sadece yeniden bağlanan yöneticiye gönder.
            await Clients.Caller.RestoreGameState(fullState);
        }
        else
        {
            await Clients.Caller.Error("Oyun bulunamadı veya sona erdi.");
        }
    }

    public async Task<bool> JoinGame(string pin, string nickname, string? sessionToken = null)
    {
        // Oyuncuyu Backend'e kaydet veya var olan oyuncunun bağlantısını güncelle
        var (player, errorMessage, newSessionToken) = await _quizService.JoinOrRejoinAsync(pin, nickname, Context.ConnectionId, sessionToken);
        
        if (player == null)
        {
            await Clients.Caller.Error(errorMessage ?? "Bilinmeyen bir hata oluştu.");
            return false;
        }

        // Yeni veya mevcut oturum token'ını öğrenciye gönder
        await Clients.Caller.SessionTokenReceived(newSessionToken!);

        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
        await Clients.Group(pin).PlayerJoined(nickname);
        return true;
    }

    // YENİ: Oyuncu veya yöneticinin bağlantısı koptuğunda tetiklenir.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (pin, nickname) = await _quizService.UnregisterPlayerAsync(Context.ConnectionId);

        if (pin != null && nickname != null)
        {
            // Diğer oyunculara ve yöneticiye oyuncunun ayrıldığı bilgisini gönder.
            await Clients.Group(pin).PlayerLeft(nickname);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // YENİ: Öğrenci kendi isteğiyle lobiden veya oyun bitişinden ayrıldığında tetiklenir.
    public async Task LeaveGame()
    {
        var (pin, nickname) = await _quizService.UnregisterPlayerAsync(Context.ConnectionId);
        if (pin != null && nickname != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, pin);
            await Clients.Group(pin).PlayerLeft(nickname);
        }
    }

    // YENİ: AŞAMA 4 - Oyunu Backend tarafında başlatır. Artık frontend index sormaz.
    [Authorize(Roles = "Host")]
    public async Task StartGame(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        
        if (quiz != null && quiz.Questions.Count > 0)
        {
            // YENİ: Soruları göndermeden önce herkese 3 saniyelik 3-2-1 sayacı başlatmasını söyle
            await Clients.Group(pin).GetReady();
            await Task.Delay(3000);

            var question = quiz.Questions[0];
            
            var secureQuestionPacket = new {
                Id = question.Id,
                Text = question.Text,
                TimeLimit = question.TimeLimitInSeconds,
                Options = question.Options.Select(o => new { o.Id, o.Text }).ToList(),
                CurrentIndex = 1,
                TotalQuestions = quiz.Questions.Count,
                TotalPlayers = quiz.Players.Count(p => !string.IsNullOrEmpty(p.ConnectionId))
            };

            // AŞAMA 4: Oyun sunucu döngüsüne eklenir
            await _quizService.StartGameFlowAsync(pin);
            await Clients.Group(pin).ReceiveQuestion(secureQuestionPacket);
        }
    }

    // YENİ: Yönetici makro kontrol ile oyunu manuel bitirmek isterse tetiklenir.
    [Authorize(Roles = "Host")]
    public async Task EndGame(string pin)
    {
        _quizService.StopGameFlow(pin); // Döngüden çıkar.
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).GameEnded(quiz?.Players.OrderByDescending(p => p.Score).ToList()!);
    }

    // YENİ: Sorular arasında (veya istenilen anda) liderlik tablosunu yansıtmak için eklendi.
    [Authorize(Roles = "Host")]
    public async Task ShowLeaderboard(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).UpdateLeaderboard(quiz?.Players.OrderByDescending(p => p.Score).ToList()!);
    }

    // YENİ: Yönetici lobiyi bekleme ekranındayken iptal eder.
    [Authorize(Roles = "Host")]
    public async Task ResetLobby(string pin)
    {
        // Herkese lobinin kapandığını bildir.
        await Clients.Group(pin).LobbyReset();
        // Sunucu tarafındaki oyunu ve durumu temizle.
        await _quizService.AbandonQuizAsync(pin);
    }

    // YENİ: Oyun bittiğinde, yönetici aynı oyuncularla yeni bir oyun başlatabilir.
    [Authorize(Roles = "Host")]
    public async Task PlayAgain(string oldPin)
    {
        var oldQuiz = _quizService.GetQuizByPin(oldPin);
        if (oldQuiz == null) return;

        // Önceki oyunun başlığıyla yeni bir quiz oluşturulur ve yeni bir PIN alınır.
        var newPin = _quizService.CreateQuiz(new Quiz 
        { 
            Title = oldQuiz.Title,
            Questions = oldQuiz.Questions.Select(q => new Question
            {
                Id = Guid.NewGuid(),
                Text = q.Text,
                TimeLimitInSeconds = q.TimeLimitInSeconds,
                Options = q.Options.Select(o => new Option
                {
                    Id = Guid.NewGuid(),
                    Text = o.Text,
                    IsCorrect = o.IsCorrect
                }).ToList()
            }).ToList()
        });

        // Yönlendirilecek oyuncuların takma ad listesi alınır.
        var playersToRedirect = oldQuiz.Players
            .Where(p => !string.IsNullOrEmpty(p.ConnectionId))
            .Select(p => p.Nickname).ToList();

        var payload = new {
            NewPin = newPin,
            Players = playersToRedirect
        };

        // Eski oyundaki tüm istemcilere (yönetici dahil) yeni oyuna geçme komutu gönderilir.
        await Clients.Group(oldPin).RedirectToNewGame(payload);
    }

    public async Task SubmitAnswer(string pin, string nickname, string questionId, string optionId)
    {
        // Girdi Doğrulaması (Validation): Geçersiz bir ID gelirse sunucunun çökmesi engellenir.
        if (!Guid.TryParse(questionId, out Guid qId) || !Guid.TryParse(optionId, out Guid oId))
        {
            await Clients.Caller.AnswerResult(false);
            return;
        }

        var (isCorrect, answeredCount, totalCount, points) = await _quizService.SubmitAnswerAsync(pin, nickname, qId, oId);
        
        // Cevabı gönderen öğrenciye sonucunu ve kazandığı puanı bildir.
        await Clients.Caller.AnswerResult(new { IsCorrect = isCorrect, Points = points });

        // Yöneticiye (ve tüm gruba) o anki cevaplanma durumunu duyur.
        await Clients.Group(pin).UpdateAnswerCount(new { AnsweredCount = answeredCount, TotalCount = totalCount });
    }
}