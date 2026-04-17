using Microsoft.AspNetCore.SignalR;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;

namespace KahootClone.Api.Hubs;

public class GameHub : Hub
{
    private readonly IQuizService _quizService;

    public GameHub(IQuizService quizService)
    {
        _quizService = quizService;
    }

    // YENİ: Yöneticinin (Host) oyuna oyuncu olarak dahil olmadan sadece gruba katılması sağlanır.
    // (Yöneticinin skor tablosunda 0 puanla listelenme hatasını düzeltir)
    public async Task JoinAsManager(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        if (quiz == null || !quiz.IsActive)
        {
            await Clients.Caller.SendAsync("Error", "Geçersiz PIN veya oyun aktif değil.");
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
    }

    // YENİ: Sayfayı yenileyen yöneticinin oyuna tekrar dahil olmasını ve oyun durumunu almasını sağlar.
    public async Task RejoinAsManager(string pin)
    {
        // Önce gruba dahil et ki yayınları alabilsin.
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);

        // Ardından oyunun tam durumunu çek.
        var fullState = _quizService.GetFullGameState(pin);
        if (fullState != null)
        {
            // Durum bilgisini sadece yeniden bağlanan yöneticiye gönder.
            await Clients.Caller.SendAsync("RestoreGameState", fullState);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Oyun bulunamadı veya sona erdi.");
        }
    }

    public async Task JoinGame(string pin, string nickname)
    {
        // Oyuncuyu Backend'e kaydet veya var olan oyuncunun bağlantısını güncelle
        var (player, errorMessage) = _quizService.JoinOrRejoin(pin, nickname, Context.ConnectionId);
        
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", errorMessage ?? "Bilinmeyen bir hata oluştu.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
        await Clients.Group(pin).SendAsync("PlayerJoined", nickname);
    }

    // YENİ: Oyuncu veya yöneticinin bağlantısı koptuğunda tetiklenir.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (pin, nickname) = _quizService.UnregisterPlayer(Context.ConnectionId);

        if (pin != null && nickname != null)
        {
            // Diğer oyunculara ve yöneticiye oyuncunun ayrıldığı bilgisini gönder.
            await Clients.Group(pin).SendAsync("PlayerLeft", nickname);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // YENİ: AŞAMA 4 - Oyunu Backend tarafında başlatır. Artık frontend index sormaz.
    public async Task StartGame(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        
        if (quiz != null && quiz.Questions.Count > 0)
        {
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
            _quizService.StartGameFlow(pin);
            await Clients.Group(pin).SendAsync("ReceiveQuestion", secureQuestionPacket);
        }
    }

    // YENİ: Yönetici makro kontrol ile oyunu manuel bitirmek isterse tetiklenir.
    public async Task EndGame(string pin)
    {
        _quizService.StopGameFlow(pin); // Döngüden çıkar.
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).SendAsync("GameEnded", quiz?.Players.OrderByDescending(p => p.Score).ToList());
    }

    // YENİ: Sorular arasında (veya istenilen anda) liderlik tablosunu yansıtmak için eklendi.
    public async Task ShowLeaderboard(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).SendAsync("UpdateLeaderboard", quiz?.Players.OrderByDescending(p => p.Score).ToList());
    }

    // YENİ: Yönetici lobiyi bekleme ekranındayken iptal eder.
    public async Task ResetLobby(string pin)
    {
        // Herkese lobinin kapandığını bildir.
        await Clients.Group(pin).SendAsync("LobbyReset");
        // Sunucu tarafındaki oyunu ve durumu temizle.
        _quizService.AbandonQuiz(pin);
    }

    // YENİ: Oyun bittiğinde, yönetici aynı oyuncularla yeni bir oyun başlatabilir.
    public async Task PlayAgain(string oldPin)
    {
        var oldQuiz = _quizService.GetQuizByPin(oldPin);
        if (oldQuiz == null) return;

        // Önceki oyunun başlığıyla yeni bir quiz oluşturulur ve yeni bir PIN alınır.
        var newPin = _quizService.CreateQuiz(new Quiz { Title = oldQuiz.Title });

        // Yönlendirilecek oyuncuların takma ad listesi alınır.
        var playersToRedirect = oldQuiz.Players.Select(p => p.Nickname).ToList();

        var payload = new {
            NewPin = newPin,
            Players = playersToRedirect
        };

        // Eski oyundaki tüm istemcilere (yönetici dahil) yeni oyuna geçme komutu gönderilir.
        await Clients.Group(oldPin).SendAsync("RedirectToNewGame", payload);
    }

    public async Task SubmitAnswer(string pin, string nickname, string questionId, string optionId)
    {
        // Girdi Doğrulaması (Validation): Geçersiz bir ID gelirse sunucunun çökmesi engellenir.
        if (!Guid.TryParse(questionId, out Guid qId) || !Guid.TryParse(optionId, out Guid oId))
        {
            await Clients.Caller.SendAsync("AnswerResult", false);
            return;
        }

        var (isCorrect, answeredCount, totalCount, points) = _quizService.SubmitAnswer(pin, nickname, qId, oId);
        
        // Cevabı gönderen öğrenciye sonucunu ve kazandığı puanı bildir.
        await Clients.Caller.SendAsync("AnswerResult", new { IsCorrect = isCorrect, Points = points });

        // Yöneticiye (ve tüm gruba) o anki cevaplanma durumunu duyur.
        await Clients.Group(pin).SendAsync("UpdateAnswerCount", new { AnsweredCount = answeredCount, TotalCount = totalCount });
    }
}