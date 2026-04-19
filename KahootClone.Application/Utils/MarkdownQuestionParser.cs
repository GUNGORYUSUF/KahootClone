using KahootClone.Application.DTOs;
using System;
using System.Collections.Generic;

namespace KahootClone.Application.Utils;

public static class MarkdownQuestionParser
{
    public static List<QuestionDto> Parse(string markdownText)
    {
        var questions = new List<QuestionDto>();
        if (string.IsNullOrWhiteSpace(markdownText))
            return questions;

        var lines = markdownText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        QuestionDto? currentQuestion = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // Yeni bir soru başlığı algılandı (Örn: "# Soru: Başkent neresidir?" veya sadece "# Başkent neresidir?")
            if (trimmedLine.StartsWith("#"))
            {
                var questionText = trimmedLine.Substring(1).Replace("Soru:", "", StringComparison.OrdinalIgnoreCase).Trim();
                currentQuestion = new QuestionDto { Text = questionText };
                questions.Add(currentQuestion);
            }
            // Süre belirteci algılandı (Örn: "Süre: 30" veya "Time: 20")
            else if ((trimmedLine.StartsWith("Süre:", StringComparison.OrdinalIgnoreCase) || 
                      trimmedLine.StartsWith("Time:", StringComparison.OrdinalIgnoreCase)) && currentQuestion != null)
            {
                var timeStr = trimmedLine.Substring(5).Trim();
                if (int.TryParse(timeStr, out int timeLimit) && timeLimit > 0)
                {
                    currentQuestion.TimeLimitInSeconds = timeLimit;
                }
            }
            // Şık algılandı (Örn: "- Paris (*)" veya "* Londra")
            else if ((trimmedLine.StartsWith("-") || trimmedLine.StartsWith("*")) && currentQuestion != null)
            {
                // Şıkkın doğru olup olmadığı evrensel (*) işaretiyle kontrol edilir
                bool isCorrect = trimmedLine.Contains("(*)");
                
                // Başındaki tireyi ve sonundaki (*) işaretini temizleyerek saf metni al
                var optionText = trimmedLine.Substring(1).Replace("(*)", "").Trim();
                
                if (!string.IsNullOrWhiteSpace(optionText))
                {
                    currentQuestion.Options.Add(new OptionDto { Text = optionText, IsCorrect = isCorrect });
                }
            }
        }
        return questions;
    }
}