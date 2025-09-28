using Aula.Configuration;
using Microsoft.Extensions.Logging;

namespace Aula.Context;

/// <summary>
/// Implementation of context validation with security checks and audit logging.
/// </summary>
public class ChildContextValidator : IChildContextValidator
{
    private readonly ILogger<ChildContextValidator> _logger;
    private readonly HashSet<string> _validOperations;

    public ChildContextValidator(ILogger<ChildContextValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Define valid operations that children can perform
        _validOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Data operations
            "read:week_letter",
            "write:week_letter",
            "read:week_schedule",
            "write:week_schedule",
            "read:database",
            "write:database",
            "delete:database",

            // Calendar operations
            "read:calendar",

            // Reminder operations
            "write:reminder",
            "read:reminder",
            "delete:reminder",

            // Communication operations
            "send:message",
            "read:conversation"
        };
    }

    public Task<bool> ValidateContextIntegrityAsync(IChildContext context)
    {
        if (context == null)
        {
            _logger.LogWarning("Context validation failed: context is null");
            return Task.FromResult(false);
        }

        try
        {
            // Check if context has been properly initialized
            if (context.CurrentChild == null)
            {
                _logger.LogWarning(
                    "Context validation failed: no child set for context {ContextId}",
                    context.ContextId);
                return Task.FromResult(false);
            }

            // Check if context ID is valid
            if (context.ContextId == Guid.Empty)
            {
                _logger.LogWarning("Context validation failed: invalid context ID");
                return Task.FromResult(false);
            }

            // Check if context creation time is reasonable (not in future, not too old)
            var now = DateTimeOffset.UtcNow;
            if (context.CreatedAt > now.AddMinutes(1)) // Allow 1 minute clock skew
            {
                _logger.LogWarning(
                    "Context validation failed: context {ContextId} created in future at {CreatedAt}",
                    context.ContextId, context.CreatedAt);
                return Task.FromResult(false);
            }

            _logger.LogDebug(
                "Context validation successful for context {ContextId} with child {ChildName}",
                context.ContextId, context.CurrentChild.FirstName);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Context validation failed with exception for context {ContextId}",
                context?.ContextId ?? Guid.Empty);
            return Task.FromResult(false);
        }
    }

    public Task<bool> ValidateChildPermissionsAsync(Child child, string operation)
    {
        if (child == null)
        {
            _logger.LogWarning("Permission validation failed: child is null");
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            _logger.LogWarning("Permission validation failed: operation is null or empty");
            return Task.FromResult(false);
        }

        // Check if the operation is in our list of valid operations
        var isValid = _validOperations.Contains(operation);

        if (isValid)
        {
            _logger.LogDebug(
                "Permission granted for child {ChildName} to perform operation {Operation}",
                child.FirstName, operation);
        }
        else
        {
            _logger.LogWarning(
                "Permission denied for child {ChildName} to perform operation {Operation}",
                child.FirstName, operation);
        }

        return Task.FromResult(isValid);
    }

    public bool ValidateContextMatchesChild(IChildContext context, Child expectedChild)
    {
        if (context?.CurrentChild == null || expectedChild == null)
        {
            _logger.LogWarning("Context match validation failed: null context or expected child");
            return false;
        }

        // Compare by first name and last name to ensure it's the same child
        var matches = string.Equals(context.CurrentChild.FirstName, expectedChild.FirstName, StringComparison.Ordinal)
                     && string.Equals(context.CurrentChild.LastName, expectedChild.LastName, StringComparison.Ordinal);

        if (!matches)
        {
            _logger.LogWarning(
                "Context match validation failed: context has {ContextChild} but expected {ExpectedChild}",
                $"{context.CurrentChild.FirstName} {context.CurrentChild.LastName}",
                $"{expectedChild.FirstName} {expectedChild.LastName}");
        }
        else
        {
            _logger.LogDebug(
                "Context match validation successful for {ChildName}",
                expectedChild.FirstName);
        }

        return matches;
    }

    public bool ValidateContextLifetime(IChildContext context, TimeSpan maxLifetime)
    {
        if (context == null)
        {
            _logger.LogWarning("Context lifetime validation failed: context is null");
            return false;
        }

        var age = DateTimeOffset.UtcNow - context.CreatedAt;
        var isValid = age <= maxLifetime;

        if (!isValid)
        {
            _logger.LogWarning(
                "Context lifetime validation failed: context {ContextId} is {Age} old, exceeds max {MaxLifetime}",
                context.ContextId, age, maxLifetime);
        }
        else
        {
            _logger.LogDebug(
                "Context lifetime validation successful for context {ContextId}, age {Age}",
                context.ContextId, age);
        }

        return isValid;
    }
}