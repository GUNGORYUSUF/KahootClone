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

    public async Task JoinGame(string pin, string nickname)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, pin);
        await Clients.Group(pin).SendAsync("PlayerJoined", nickname);
    }

    // YENİ: Otomatik akış için sıradaki soru (index) istenir.
    public async Task AskQuestion(string pin, int questionIndex)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        
        // Eğer istenen sıradaki soru mevcutsa öğrencilere fırlatılır.
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

            await Clients.Group(pin).SendAsync("ReceiveQuestion", secureQuestionPacket);
        }
        else
        {
            // Sorular bittiyse oyun otomatik olarak bitirilir ve skorlar gönderilir.
            await Clients.Group(pin).SendAsync("GameEnded", quiz?.Players.OrderByDescending(p => p.Score).ToList());
        }
    }

    // YENİ: Öğretmen makro kontrol ile oyunu manuel bitirmek isterse tetiklenir.
    public async Task EndGame(string pin)
    {
        var quiz = _quizService.GetQuizByPin(pin);
        await Clients.Group(pin).SendAsync("GameEnded", quiz?.Players.OrderByDescending(p => p.Score).ToList());
    }

    public async Task SubmitAnswer(string pin, string nickname, string questionId, string optionId)
    {
        bool isCorrect = _quizService.SubmitAnswer(pin, nickname, Guid.Parse(questionId), Guid.Parse(optionId));
        await Clients.Caller.SendAsync("AnswerResult", isCorrect);
    }
}