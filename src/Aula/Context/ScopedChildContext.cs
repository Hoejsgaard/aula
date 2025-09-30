using Aula.Configuration;
using Microsoft.Extensions.Logging;

namespace Aula.Context;

/// <summary>
/// Scoped implementation of IChildContext with secure lifetime management.
/// This class ensures child context is immutable once set and properly disposed.
/// </summary>
public class ScopedChildContext : IChildContext, IDisposable
{
	private readonly ILogger<ScopedChildContext> _logger;
	private readonly object _lock = new();
	private bool _disposed;
	private Child? _currentChild;

	public ScopedChildContext(ILogger<ScopedChildContext> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);
		_logger = logger;
		ContextId = Guid.NewGuid();
		CreatedAt = DateTimeOffset.UtcNow;

		_logger.LogDebug("Created child context scope {ContextId}", ContextId);
	}

	public Child? CurrentChild
	{
		get
		{
			lock (_lock)
			{
				ThrowIfDisposed();
				return _currentChild;
			}
		}
		private set => _currentChild = value;
	}

	public Guid ContextId { get; }

	public DateTimeOffset CreatedAt { get; }

	public void SetChild(Child child)
	{
		ArgumentNullException.ThrowIfNull(child);

		lock (_lock)
		{
			ThrowIfDisposed();

			if (_currentChild != null)
			{
				_logger.LogError(
					"Attempted to set child {NewChild} when context already has {ExistingChild} for scope {ContextId}",
					child.FirstName, _currentChild.FirstName, ContextId);
				throw new InvalidOperationException(
					$"Child context already set for scope {ContextId}. Context is immutable once initialized.");
			}

			_currentChild = child;
			_logger.LogInformation(
				"Set child context to {ChildName} for scope {ContextId}",
				child.FirstName, ContextId);
		}
	}

	public void ClearChild()
	{
		lock (_lock)
		{
			if (_currentChild != null)
			{
				_logger.LogDebug(
					"Clearing child context {ChildName} from scope {ContextId}",
					_currentChild.FirstName, ContextId);
				_currentChild = null;
			}
		}
	}

	public void ValidateContext()
	{
		lock (_lock)
		{
			ThrowIfDisposed();

			if (_currentChild == null)
			{
				_logger.LogError("No child context set for scope {ContextId}", ContextId);
				throw new InvalidOperationException(
					$"No child context set for scope {ContextId}. Ensure SetChild is called before accessing child-aware services.");
			}
		}
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
			lock (_lock)
			{
				if (_currentChild != null)
				{
					_logger.LogDebug(
						"Disposing child context {ChildName} for scope {ContextId}",
						_currentChild.FirstName, ContextId);
					ClearChild();
				}

				_disposed = true;
			}
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(
				nameof(ScopedChildContext),
				$"Child context scope {ContextId} has been disposed");
		}
	}
}
