using Aula.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aula.Context;

public class ChildContextScope : IDisposable
{
	private readonly IServiceScope _scope;
	private readonly IChildContext _context;
	private readonly ILogger<ChildContextScope> _logger;
	private bool _disposed;

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

	public IServiceProvider ServiceProvider => _scope.ServiceProvider;

	public IChildContext Context => _context;

	public Child Child => _context.CurrentChild ??
		throw new InvalidOperationException("Child context not properly initialized");

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
