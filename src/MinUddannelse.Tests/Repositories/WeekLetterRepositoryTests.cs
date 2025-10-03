using MinUddannelse.Repositories;
using MinUddannelse.AI.Services;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Security;
using MinUddannelse;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using Xunit;

namespace MinUddannelse.Tests.Repositories;

public class WeekLetterRepositoryTests
{
    private readonly ILoggerFactory _loggerFactory;

    public WeekLetterRepositoryTests()
    {
        _loggerFactory = new LoggerFactory();
    }

    [Fact]
    public void Constructor_WithNullSupabaseClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WeekLetterRepository(null!, _loggerFactory));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WeekLetterRepository(null!, null!));
    }

    [Fact]
    public void Repository_ImplementsIWeekLetterRepositoryInterface()
    {
        Assert.True(typeof(IWeekLetterRepository).IsAssignableFrom(typeof(WeekLetterRepository)));
    }

    [Fact]
    public void Repository_HasCorrectPublicMethods()
    {
        var repositoryType = typeof(WeekLetterRepository);

        Assert.NotNull(repositoryType.GetMethod("HasWeekLetterBeenPostedAsync"));
        Assert.NotNull(repositoryType.GetMethod("MarkWeekLetterAsPostedAsync"));
        Assert.NotNull(repositoryType.GetMethod("StoreWeekLetterAsync"));
        Assert.NotNull(repositoryType.GetMethod("GetStoredWeekLetterAsync"));
        Assert.NotNull(repositoryType.GetMethod("GetStoredWeekLettersAsync"));
        Assert.NotNull(repositoryType.GetMethod("GetLatestStoredWeekLetterAsync"));
        Assert.NotNull(repositoryType.GetMethod("DeleteWeekLetterAsync"));
    }

    [Fact]
    public void Repository_IsPublicClass()
    {
        var repositoryType = typeof(WeekLetterRepository);

        Assert.True(repositoryType.IsPublic);
        Assert.False(repositoryType.IsAbstract);
        Assert.False(repositoryType.IsSealed);
    }


    [Fact]
    public void WeekLetterRepository_HasCorrectNamespace()
    {
        var repositoryType = typeof(WeekLetterRepository);
        Assert.Equal("MinUddannelse.Repositories", repositoryType.Namespace);
    }

    [Fact]
    public void WeekLetterRepository_HasCorrectConstructorParameters()
    {
        var repositoryType = typeof(WeekLetterRepository);
        var constructor = repositoryType.GetConstructors()[0];
        var parameters = constructor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("supabase", parameters[0].Name);
        Assert.Equal("loggerFactory", parameters[1].Name);
    }

    [Fact]
    public void StoredWeekLetter_HasRequiredProperties()
    {
        var storedWeekLetter = new StoredWeekLetter
        {
            ChildName = "Emma",
            WeekNumber = 1,
            Year = 2024,
            RawContent = "test content",
            PostedAt = DateTime.Now
        };

        Assert.Equal("Emma", storedWeekLetter.ChildName);
        Assert.Equal(1, storedWeekLetter.WeekNumber);
        Assert.Equal(2024, storedWeekLetter.Year);
        Assert.Equal("test content", storedWeekLetter.RawContent);
        Assert.True(storedWeekLetter.PostedAt <= DateTime.Now);
    }

    [Fact]
    public void PostedLetter_HasRequiredProperties()
    {
        var postedLetter = new PostedLetter
        {
            Id = 1,
            ChildName = "Emma",
            WeekNumber = 1,
            Year = 2024,
            ContentHash = "hash123",
            PostedAt = DateTime.Now,
            PostedToSlack = true,
            PostedToTelegram = false,
            RawContent = "test content"
        };

        Assert.Equal(1, postedLetter.Id);
        Assert.Equal("Emma", postedLetter.ChildName);
        Assert.Equal(1, postedLetter.WeekNumber);
        Assert.Equal(2024, postedLetter.Year);
        Assert.Equal("hash123", postedLetter.ContentHash);
        Assert.True(postedLetter.PostedToSlack);
        Assert.False(postedLetter.PostedToTelegram);
        Assert.Equal("test content", postedLetter.RawContent);
    }
}