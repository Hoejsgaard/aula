using MinUddannelse.Configuration;
using MinUddannelse.Repositories;
using MinUddannelse.Repositories.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MinUddannelse.Tests.Repositories;

public class RetryTrackingRepositoryTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Config _config;

    public RetryTrackingRepositoryTests()
    {
        _loggerFactory = new LoggerFactory();
        _config = new Config
        {
            WeekLetter = new WeekLetter
            {
                RetryIntervalHours = 2,
                MaxRetryDurationHours = 48
            }
        };
    }

    [Fact]
    public void Constructor_WithNullSupabaseClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RetryTrackingRepository(null!, _loggerFactory, _config));
    }

    [Fact]
    public void Repository_ImplementsIRetryTrackingRepositoryInterface()
    {
        Assert.True(typeof(IRetryTrackingRepository).IsAssignableFrom(typeof(RetryTrackingRepository)));
    }

    [Fact]
    public void Repository_HasCorrectPublicMethods()
    {
        var repositoryType = typeof(RetryTrackingRepository);

        Assert.NotNull(repositoryType.GetMethod("GetRetryAttemptsAsync"));
        Assert.NotNull(repositoryType.GetMethod("IncrementRetryAttemptAsync"));
        Assert.NotNull(repositoryType.GetMethod("MarkRetryAsSuccessfulAsync"));
        Assert.NotNull(repositoryType.GetMethod("GetPendingRetriesAsync"));
    }

    [Fact]
    public void GetPendingRetriesAsync_HasCorrectMethodSignature()
    {
        var method = typeof(RetryTrackingRepository).GetMethod("GetPendingRetriesAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<List<RetryAttempt>>), method.ReturnType);
        Assert.Empty(method.GetParameters()); // No parameters
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void GetPendingRetriesAsync_IsAsyncMethod()
    {
        var method = typeof(RetryTrackingRepository).GetMethod("GetPendingRetriesAsync");

        Assert.NotNull(method);
        Assert.True(method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false).Length > 0 ||
                   method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    [Fact]
    public void Interface_ContainsGetPendingRetriesAsyncMethod()
    {
        var interfaceType = typeof(IRetryTrackingRepository);
        var method = interfaceType.GetMethod("GetPendingRetriesAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<List<RetryAttempt>>), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void RetryAttempt_HasRequiredProperties()
    {
        var retryAttemptType = typeof(RetryAttempt);

        Assert.NotNull(retryAttemptType.GetProperty("ChildName"));
        Assert.NotNull(retryAttemptType.GetProperty("WeekNumber"));
        Assert.NotNull(retryAttemptType.GetProperty("Year"));
        Assert.NotNull(retryAttemptType.GetProperty("AttemptCount"));
        Assert.NotNull(retryAttemptType.GetProperty("NextAttempt"));
        Assert.NotNull(retryAttemptType.GetProperty("MaxAttempts"));
    }


    [Fact]
    public void Repository_ImplementsAllInterfaceMethods()
    {
        var interfaceType = typeof(IRetryTrackingRepository);
        var implementationType = typeof(RetryTrackingRepository);

        foreach (var interfaceMethod in interfaceType.GetMethods())
        {
            var implementationMethod = implementationType.GetMethod(interfaceMethod.Name,
                interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray());

            Assert.NotNull(implementationMethod);
            Assert.True(implementationMethod.IsPublic);
        }
    }
}
