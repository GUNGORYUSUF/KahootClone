using Microsoft.AspNetCore.SignalR;
using KahootClone.Application.Interfaces;

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

    public async Task JoinGame(string pin, string nickname)
    {
        // Oyuncuyu Backend'e kaydet veya var olan oyuncunun bağlantısını güncelle
        var player = _quizService.JoinOrRejoin(pin, nickname, Context.ConnectionId);
        
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Geçersiz PIN veya oyun aktif değil.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
        await Clients.Group(pin).SendAsync("PlayerJoined", nickname);
    }

    // YENİ: Oyuncu veya yöneticinin bağlantısı koptuğunda tetiklenir.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // İlerleyen aşamalarda: Bağlantısı kopan oyuncunun oyundan düşürülmesi
        // veya yöneticiye "Bağlantı koptu" bilgisi geçilmesi buraya eklenecek.
        await base.OnDisconnectedAsync(exception);
    }

    // YENİ: Otomatik akış için sıradaki soru (index) istenir.
    public async Task AskQuestion(string pin, int questionIndex)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        
        // Eğer istenen sıradaki soru mevcutsa oyunculara gönderilir.
        if (quiz != null && quiz.Questions.Count > questionIndex)
        {
            var question = quiz.Questions[questionIndex];
            
            var secureQuestionPacket = new {
                Id = question.Id,
                Text = question.Text,
                TimeLimit = question.TimeLimitInSeconds,
                Options = question.Options.Select(o => new { o.Id, o.Text }).ToList(),
                CurrentIndex = questionIndex + 1,
                TotalQuestions = quiz.Questions.Count
            };

            // Zamanlayıcı başlatılır.
            _quizService.StartQuestion(pin);
            await Clients.Group(pin).SendAsync("ReceiveQuestion", secureQuestionPacket);
        }
        else
        {
            // Sorular bittiyse oyun otomatik olarak bitirilir ve skorlar gönderilir.
            await Clients.Group(pin).SendAsync("GameEnded", quiz?.Players.OrderByDescending(p => p.Score).ToList());
        }
    }

    // YENİ: Yönetici makro kontrol ile oyunu manuel bitirmek isterse tetiklenir.
    public async Task EndGame(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).SendAsync("GameEnded", quiz?.Players.OrderByDescending(p => p.Score).ToList());
    }

    // YENİ: Sorular arasında (veya istenilen anda) liderlik tablosunu yansıtmak için eklendi.
    public async Task ShowLeaderboard(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).SendAsync("UpdateLeaderboard", quiz?.Players.OrderByDescending(p => p.Score).ToList());
    }

    public async Task SubmitAnswer(string pin, string nickname, string questionId, string optionId)
    {
        // Girdi Doğrulaması (Validation): Geçersiz bir ID gelirse sunucunun çökmesi engellenir.
        if (!Guid.TryParse(questionId, out Guid qId) || !Guid.TryParse(optionId, out Guid oId))
        {
            await Clients.Caller.SendAsync("AnswerResult", false);
            return;
        }

        bool isCorrect = _quizService.SubmitAnswer(pin, nickname, qId, oId);
        await Clients.Caller.SendAsync("AnswerResult", isCorrect);
    }
}