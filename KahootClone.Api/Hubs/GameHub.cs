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
        // Girdi Doğrulaması (Validation)
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

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
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

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

    public async Task<bool> JoinGame(string pin, string nickname, string? sessionToken = null, string? googleToken = null, string? avatarUrl = null)
    {
        // Girdi Doğrulaması (DoS ve Buffer Overflow koruması)
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return false;
        if (string.IsNullOrEmpty(nickname) || nickname.Length > 30) return false;
        if (avatarUrl != null && avatarUrl.Length > 2000) return false;

        // Oyuncuyu Backend'e kaydet veya var olan oyuncunun bağlantısını güncelle
        var (player, errorMessage, newSessionToken) = await _quizService.JoinOrRejoinAsync(pin, nickname, Context.ConnectionId, sessionToken, googleToken, avatarUrl);
        
        if (player == null)
        {
            await Clients.Caller.Error(errorMessage ?? "Bilinmeyen bir hata oluştu.");
            return false;
        }

        // Yeni veya mevcut oturum token'ını öğrenciye gönder
        await Clients.Caller.SessionTokenReceived(newSessionToken!);

        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
        await Clients.Group(pin).PlayerJoined(new { Nickname = nickname, AvatarUrl = player.AvatarUrl });
        return true;
    }

    // YENİ: Oyuncu veya yöneticinin bağlantısı koptuğunda tetiklenir.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var (pin, nickname) = await _quizService.UnregisterPlayerAsync(Context.ConnectionId);

            if (pin != null && nickname != null)
            {
                // Diğer oyunculara ve yöneticiye oyuncunun ayrıldığı bilgisini gönder.
                await Clients.Group(pin).PlayerLeft(nickname);
            }
        }
        catch
        {
            // Sessizce yut ki SignalR bağlantı kapama (Close) mesajında sunucu hatası fırlatmasın.
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

    // YENİ: Yönetici lobideyken istenmeyen bir oyuncuyu atar.
    [Authorize(Roles = "Host")]
    public async Task KickPlayer(string pin, string nickname)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;
        if (string.IsNullOrEmpty(nickname) || nickname.Length > 30) return;

        var connectionId = await _quizService.KickPlayerAsync(pin, nickname);
        if (!string.IsNullOrEmpty(connectionId))
        {
            await Groups.RemoveFromGroupAsync(connectionId, pin);
            await Clients.Client(connectionId).Kicked();
            await Clients.Group(pin).PlayerLeft(nickname);
        }
    }

    // YENİ: AŞAMA 4 - Oyunu Backend tarafında başlatır. Artık frontend index sormaz.
    [Authorize(Roles = "Host")]
    public async Task StartGame(string pin)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

        var quiz = _quizService.GetQuizByPin(pin);
        
        if (quiz != null && quiz.Questions.Count > 0)
        {
            // Idempotency (Etkisizlik) Koruması: Yönetici ağ gecikmesi nedeniyle "Başlat" tuşuna çift tıklarsa
            // aynı oyunun iki kez başlatılması (Race Condition) engellenir.
            if (quiz.CurrentQuestionStartTime != default) return;

            // Hub Bloklama İhlali (Task.Delay) Giderildi: Sunucu thread'ini kilitlemek yerine işlem serbest bırakıldı.
            await Clients.Group(pin).GetReady();

            // MİMARİ DÜZELTME: İlk soruyu anında fırlatmak yerine sadece döngüyü başlatıyoruz.
            // Arka plandaki State Machine 3-2-1 sayımını yapıp ilk soruyu kendisi fırlatacak!
            await _quizService.StartGameFlowAsync(pin);
        }
    }

    // YENİ: Yönetici makro kontrol ile oyunu manuel bitirmek isterse tetiklenir.
    [Authorize(Roles = "Host")]
    public async Task EndGame(string pin)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

        _quizService.StopGameFlow(pin); // Döngüden çıkar.
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).GameEnded(quiz?.Players.OrderByDescending(p => p.Score).ToList()!);
    }

    // YENİ: Sorular arasında (veya istenilen anda) liderlik tablosunu yansıtmak için eklendi.
    [Authorize(Roles = "Host")]
    public async Task ShowLeaderboard(string pin)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).UpdateLeaderboard(quiz?.Players.OrderByDescending(p => p.Score).ToList()!);
    }

    // YENİ: Yönetici lobiyi bekleme ekranındayken iptal eder.
    [Authorize(Roles = "Host")]
    public async Task ResetLobby(string pin)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;

        // Herkese lobinin kapandığını bildir.
        await Clients.Group(pin).LobbyReset();
        // Sunucu tarafındaki oyunu ve durumu temizle.
        await _quizService.AbandonQuizAsync(pin);
    }

    // YENİ: Oyun bittiğinde, yönetici aynı oyuncularla yeni bir oyun başlatabilir.
    [Authorize(Roles = "Host")]
    public async Task PlayAgain(string oldPin)
    {
        // Girdi Doğrulaması
        if (string.IsNullOrEmpty(oldPin) || oldPin.Length > 10) return;

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
            .Select(p => new { Nickname = p.Nickname, AvatarUrl = p.AvatarUrl }).ToList();

        var payload = new {
            NewPin = newPin,
            Players = playersToRedirect
        };

        // Eski oyundaki tüm istemcilere (yönetici dahil) yeni oyuna geçme komutu gönderilir.
        await Clients.Group(oldPin).RedirectToNewGame(payload);
    }

    public async Task SubmitAnswer(string pin, string nickname, string questionId, string optionId)
    {
        // Girdi Doğrulaması (Validation): Sınırsız girdi engeli
        if (string.IsNullOrEmpty(pin) || pin.Length > 10) return;
        if (string.IsNullOrEmpty(nickname) || nickname.Length > 30) return;

        // Girdi Doğrulaması (Validation): Geçersiz bir ID gelirse sunucunun çökmesi engellenir.
        if (!Guid.TryParse(questionId, out Guid qId) || !Guid.TryParse(optionId, out Guid oId))
        {
            await Clients.Caller.AnswerResult(false);
            return;
        }

        // Kimlik Sahtekarlığı (Identity Spoofing) Koruması: Context.ConnectionId eşleşmesi zorunludur.
        var quiz = _quizService.GetQuizByPin(pin);
        var player = quiz?.Players.FirstOrDefault(p => p.Nickname == nickname);
        if (player == null || player.ConnectionId != Context.ConnectionId)
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