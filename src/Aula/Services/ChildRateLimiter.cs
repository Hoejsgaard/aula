using Aula.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aula.Services;

/// <summary>
/// Rate limiting implementation that tracks and enforces operation limits per child.
/// Uses a sliding window algorithm to prevent DoS attacks and resource exhaustion.
/// </summary>
public class ChildRateLimiter : IChildRateLimiter
{
    private readonly ILogger<ChildRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, RateLimitState> _limitStates;
    private readonly Dictionary<string, RateLimitConfig> _operationLimits;

    public ChildRateLimiter(ILogger<ChildRateLimiter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _limitStates = new ConcurrentDictionary<string, RateLimitState>();

        // Configure rate limits per operation type
        _operationLimits = new Dictionary<string, RateLimitConfig>
        {
            // Cache operations - higher limits as they're lightweight
            { "CacheWeekLetter", new RateLimitConfig(100, TimeSpan.FromMinutes(1)) },
            { "GetWeekLetter", new RateLimitConfig(100, TimeSpan.FromMinutes(1)) },
            { "CacheWeekSchedule", new RateLimitConfig(100, TimeSpan.FromMinutes(1)) },
            { "GetWeekSchedule", new RateLimitConfig(100, TimeSpan.FromMinutes(1)) },

            // Database operations - lower limits as they're more expensive
            { "StoreWeekLetter", new RateLimitConfig(10, TimeSpan.FromMinutes(1)) },
            { "DeleteWeekLetter", new RateLimitConfig(5, TimeSpan.FromMinutes(10)) }, // Destructive operation
            { "GetStoredWeekLetters", new RateLimitConfig(20, TimeSpan.FromMinutes(1)) },

            // Default for unknown operations
            { "default", new RateLimitConfig(50, TimeSpan.FromMinutes(1)) }
        };
    }

    public Task<bool> IsAllowedAsync(Child child, string operation)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        var key = GetRateLimitKey(child, operation);
        var config = GetOperationConfig(operation);
        var now = DateTimeOffset.UtcNow;

        var state = _limitStates.AddOrUpdate(key,
            _ => new RateLimitState(config.WindowDuration),
            (_, existing) => existing);

        lock (state)
        {
            // Remove expired operations outside the window
            state.RemoveExpiredOperations(now);

            // Check if limit would be exceeded
            if (state.OperationCount >= config.LimitPerWindow)
            {
                _logger.LogWarning("Rate limit exceeded for {ChildName} operation {Operation}: {Count}/{Limit}",
                    child.FirstName, operation, state.OperationCount, config.LimitPerWindow);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }

    public Task RecordOperationAsync(Child child, string operation)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        var key = GetRateLimitKey(child, operation);
        var config = GetOperationConfig(operation);
        var now = DateTimeOffset.UtcNow;

        var state = _limitStates.AddOrUpdate(key,
            _ => new RateLimitState(config.WindowDuration),
            (_, existing) => existing);

        lock (state)
        {
            state.RecordOperation(now);
            _logger.LogDebug("Recorded operation {Operation} for {ChildName}. Count: {Count}",
                operation, child.FirstName, state.OperationCount);
        }

        return Task.CompletedTask;
    }

    public Task<int> GetRemainingOperationsAsync(Child child, string operation)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        var key = GetRateLimitKey(child, operation);
        var config = GetOperationConfig(operation);
        var now = DateTimeOffset.UtcNow;

        if (_limitStates.TryGetValue(key, out var state))
        {
            lock (state)
            {
                state.RemoveExpiredOperations(now);
                var remaining = Math.Max(0, config.LimitPerWindow - state.OperationCount);
                return Task.FromResult(remaining);
            }
        }

        return Task.FromResult(config.LimitPerWindow);
    }

    public Task ResetLimitsAsync(Child child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        var keysToRemove = _limitStates.Keys
            .Where(k => k.StartsWith($"{child.FirstName}_{child.LastName}_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _limitStates.TryRemove(key, out _);
        }

        _logger.LogInformation("Reset rate limits for {ChildName}. Removed {Count} limit states",
            child.FirstName, keysToRemove.Count);

        return Task.CompletedTask;
    }

    private string GetRateLimitKey(Child child, string operation)
    {
        return $"{child.FirstName}_{child.LastName}_{operation}".ToLowerInvariant();
    }

    private RateLimitConfig GetOperationConfig(string operation)
    {
        return _operationLimits.TryGetValue(operation, out var config)
            ? config
            : _operationLimits["default"];
    }

    /// <summary>
    /// Configuration for a rate limit.
    /// </summary>
    private class RateLimitConfig
    {
        public int LimitPerWindow { get; }
        public TimeSpan WindowDuration { get; }

        public RateLimitConfig(int limitPerWindow, TimeSpan windowDuration)
        {
            LimitPerWindow = limitPerWindow;
            WindowDuration = windowDuration;
        }
    }

    /// <summary>
    /// State tracking for rate limiting using sliding window algorithm.
    /// </summary>
    private class RateLimitState
    {
        private readonly TimeSpan _windowDuration;
        private readonly Queue<DateTimeOffset> _operationTimestamps;

        public RateLimitState(TimeSpan windowDuration)
        {
            _windowDuration = windowDuration;
            _operationTimestamps = new Queue<DateTimeOffset>();
        }

        public int OperationCount => _operationTimestamps.Count;

        public void RecordOperation(DateTimeOffset timestamp)
        {
            _operationTimestamps.Enqueue(timestamp);
        }

        public void RemoveExpiredOperations(DateTimeOffset now)
        {
            var windowStart = now - _windowDuration;

            while (_operationTimestamps.Count > 0 && _operationTimestamps.Peek() < windowStart)
            {
                _operationTimestamps.Dequeue();
            }
        }
    }
}