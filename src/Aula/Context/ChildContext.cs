using Aula.Configuration;
using Microsoft.Extensions.Logging;

namespace Aula.Context;

/// <summary>
/// Implementation of IChildContext that holds the current child context.
/// Used in scoped dependency injection to provide child-specific services.
/// </summary>
public class ChildContext : IChildContext
{
    private readonly ILogger<ChildContext> _logger;
    private Child? _currentChild;
    private readonly Guid _contextId;
    private readonly DateTimeOffset _createdAt;
    private bool _isChildSet;

    public ChildContext(ILogger<ChildContext> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextId = Guid.NewGuid();
        _createdAt = DateTimeOffset.UtcNow;
        _isChildSet = false;
    }

    public Child? CurrentChild => _currentChild;

    public Guid ContextId => _contextId;

    public DateTimeOffset CreatedAt => _createdAt;

    public void SetChild(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        if (_isChildSet)
        {
            throw new InvalidOperationException(
                $"Child context already set to {_currentChild?.FirstName}. Context is immutable once initialized.");
        }

        _currentChild = child;
        _isChildSet = true;
        _logger.LogDebug("Child context set to {ChildName} in context {ContextId}",
            child.FirstName, _contextId);
    }

    public void ClearChild()
    {
        _logger.LogDebug("Clearing child context {ContextId}", _contextId);
        _currentChild = null;
        _isChildSet = false;
    }

    public void ValidateContext()
    {
        if (_currentChild == null || !_isChildSet)
        {
            throw new InvalidOperationException("Child context is not set. Ensure operations are executed within a child scope.");
        }
    }
}