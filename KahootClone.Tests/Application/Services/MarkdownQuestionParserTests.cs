using System.Linq;
using Xunit;
using KahootClone.Application.Utils;

namespace KahootClone.Tests.Application.Utils;

public class MarkdownQuestionParserTests
{
    [Fact]
    public void Parse_ValidMarkdown_ReturnsCorrectQuestionsList()
    {
        // Arrange
        string markdown = @"
        # Soru: Python dilinin yaratıcısı kimdir?
        Süre: 30
        - Guido van Rossum (*)
        - Linus Torvalds
        - Bill Gates
        - James Gosling

        # Fransa'nın başkenti neresidir?
        - Lyon
        * Paris (*)
        - Marsilya
        ";

        // Act
        var questions = MarkdownQuestionParser.Parse(markdown);

        // Assert
        Assert.Equal(2, questions.Count);
        
        // İlk Soru Kontrolleri
        Assert.Equal("Python dilinin yaratıcısı kimdir?", questions[0].Text);
        Assert.Equal(30, questions[0].TimeLimitInSeconds);
        Assert.Equal(4, questions[0].Options.Count);
        Assert.True(questions[0].Options.First(o => o.Text == "Guido van Rossum").IsCorrect);
        Assert.False(questions[0].Options.First(o => o.Text == "Linus Torvalds").IsCorrect);

        // İkinci Soru Kontrolleri (Süre belirtilmemiş, varsayılan 20 olmalı ve '#' sonrası metin doğru okunmalı)
        Assert.Equal("Fransa'nın başkenti neresidir?", questions[1].Text);
        Assert.Equal(20, questions[1].TimeLimitInSeconds); // Default value from DTO
        Assert.Equal(3, questions[1].Options.Count);
        Assert.True(questions[1].Options.First(o => o.Text == "Paris").IsCorrect);
    }

    [Fact]
    public void Parse_EmptyOrNullString_ReturnsEmptyList()
    {
        var nullResult = MarkdownQuestionParser.Parse(null!);
        var emptyResult = MarkdownQuestionParser.Parse("   ");

        Assert.Empty(nullResult);
        Assert.Empty(emptyResult);
    }
}