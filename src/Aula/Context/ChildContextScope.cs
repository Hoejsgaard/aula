using Aula.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aula.Context;

/// <summary>
/// Provides explicit control over child context lifetime and service scope creation.
/// This class enables creation of isolated child operation scopes with proper disposal.
/// </summary>
public class ChildContextScope : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IChildContext _context;
    private readonly ILogger<ChildContextScope> _logger;
    private bool _disposed;

    /// <summary>
    /// Creates a new child context scope with an initialized child context.
    /// </summary>
    /// <param name="serviceProvider">The root service provider</param>
    /// <param name="child">The child to set for this scope</param>
    /// <exception cref="ArgumentNullException">If serviceProvider or child is null</exception>
    public ChildContextScope(IServiceProvider serviceProvider, Child child)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(child);

        _scope = serviceProvider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<IChildContext>();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<ChildContextScope>>();

        try
        {
            _context.SetChild(child);
            _logger.LogDebug(
                "Created child context scope {ContextId} for {ChildName}",
                _context.ContextId, child.FirstName);
        }
        catch
        {
            _scope.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the scoped service provider for this child context.
    /// All services resolved from this provider will share the same child context.
    /// </summary>
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    /// <summary>
    /// Gets the child context for this scope.
    /// </summary>
    public IChildContext Context => _context;

    /// <summary>
    /// Gets the child associated with this scope.
    /// </summary>
    public Child Child => _context.CurrentChild ??
        throw new InvalidOperationException("Child context not properly initialized");

    /// <summary>
    /// Executes an operation within this child context scope.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <returns>The result of the operation</returns>
    public async Task<T> ExecuteAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        ThrowIfDisposed();
        _context.ValidateContext();

        try
        {
            _logger.LogDebug(
                "Executing operation in child context scope {ContextId} for {ChildName}",
                _context.ContextId, Child.FirstName);

            return await operation(ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Operation failed in child context scope {ContextId} for {ChildName}",
                _context.ContextId, Child.FirstName);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation within this child context scope without a return value.
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync(async provider =>
        {
            await operation(provider);
            return 0; // Dummy return value
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _logger.LogDebug(
                "Disposing child context scope {ContextId}",
                _context.ContextId);

            _scope?.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(ChildContextScope),
                $"Child context scope {_context.ContextId} has been disposed");
        }
    }
}