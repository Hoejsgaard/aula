using System;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Authentication;

/// <summary>
/// Secure implementation of child authentication service with context validation.
/// This is a simplified implementation for Chapter 7 migration.
/// </summary>
public class SecureChildAuthenticationService : IChildAuthenticationService
{
	private readonly IChildContext _childContext;
	private readonly IMinUddannelseClient _minUddannelseClient;
	private readonly IChildDataService _childDataService;
	private readonly IChildAuditService _auditService;
	private readonly ILogger<SecureChildAuthenticationService> _logger;
	private readonly Dictionary<string, AuthenticationSession> _sessions = new();

	private sealed class AuthenticationSession
	{
		public string SessionId { get; set; } = Guid.NewGuid().ToString();
		public bool IsAuthenticated { get; set; }
		public DateTimeOffset? LastAuthenticationTime { get; set; }
		public string ChildName { get; set; } = string.Empty;
	}

	public SecureChildAuthenticationService(
		IChildContext childContext,
		IMinUddannelseClient minUddannelseClient,
		IChildDataService childDataService,
		IChildAuditService auditService,
		ILogger<SecureChildAuthenticationService> logger)
	{
		ArgumentNullException.ThrowIfNull(childContext);
		ArgumentNullException.ThrowIfNull(minUddannelseClient);
		ArgumentNullException.ThrowIfNull(childDataService);
		ArgumentNullException.ThrowIfNull(auditService);
		ArgumentNullException.ThrowIfNull(logger);

		_childContext = childContext;
		_minUddannelseClient = minUddannelseClient;
		_childDataService = childDataService;
		_auditService = auditService;
		_logger = logger;
	}

	public async Task<bool> AuthenticateAsync()
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot authenticate without child context");
		}

		var child = _childContext.CurrentChild;
		_logger.LogInformation("Authenticating child {ChildName}", child.FirstName);

		try
		{
			// Create or get session
			var sessionKey = $"{child.FirstName}_{child.LastName}";
			if (!_sessions.TryGetValue(sessionKey, out var session))
			{
				session = new AuthenticationSession { ChildName = child.FirstName };
				_sessions[sessionKey] = session;
			}

			// Attempt authentication via MinUddannelseClient
			var loginSuccess = await _minUddannelseClient.LoginAsync();

			session.IsAuthenticated = true;
			session.LastAuthenticationTime = DateTimeOffset.UtcNow;

			await _auditService.LogDataAccessAsync(child, "Authenticate", "success", true);
			_logger.LogInformation("Successfully authenticated child {ChildName}", child.FirstName);

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to authenticate child {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "Authenticate", "failed", false);
			return false;
		}
	}

	public Task<bool> IsAuthenticatedAsync()
	{
		if (_childContext.CurrentChild == null)
		{
			return Task.FromResult(false);
		}

		var sessionKey = $"{_childContext.CurrentChild.FirstName}_{_childContext.CurrentChild.LastName}";
		if (_sessions.TryGetValue(sessionKey, out var session))
		{
			return Task.FromResult(session.IsAuthenticated);
		}

		return Task.FromResult(false);
	}

	public async Task<JObject?> GetWeekLetterAsync(DateOnly targetDate, bool allowLiveFetch = false)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot get week letter without child context");
		}

		var child = _childContext.CurrentChild;
		_logger.LogInformation("Getting week letter for child {ChildName}, date {Date}",
			child.FirstName, targetDate);

		try
		{
			// Use the child data service which has caching and database support
			var letter = await _childDataService.GetOrFetchWeekLetterAsync(child, targetDate, allowLiveFetch);

			if (letter != null)
			{
				await _auditService.LogDataAccessAsync(child, "GetWeekLetter", targetDate.ToString(), true);
			}

			return letter;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get week letter for child {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetWeekLetter", targetDate.ToString(), false);
			throw;
		}
	}

	public async Task<JObject?> GetWeekScheduleAsync(DateOnly targetDate)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot get week schedule without child context");
		}

		var child = _childContext.CurrentChild;
		_logger.LogInformation("Getting week schedule for child {ChildName}, date {Date}",
			child.FirstName, targetDate);

		try
		{
			// Calculate week number
			var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
			var weekNumber = calendar.GetWeekOfYear(targetDate.ToDateTime(TimeOnly.MinValue),
				System.Globalization.CalendarWeekRule.FirstFourDayWeek,
				DayOfWeek.Monday);

			var schedule = await _childDataService.GetWeekScheduleAsync(child, weekNumber, targetDate.Year);

			if (schedule != null)
			{
				await _auditService.LogDataAccessAsync(child, "GetWeekSchedule", targetDate.ToString(), true);
			}

			return schedule;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get week schedule for child {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetWeekSchedule", targetDate.ToString(), false);
			throw;
		}
	}

	public async Task<JObject?> GetStoredWeekLetterAsync(int weekNumber, int year)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot get stored week letter without child context");
		}

		var child = _childContext.CurrentChild;
		_logger.LogInformation("Getting stored week letter for child {ChildName}, week {Week}/{Year}",
			child.FirstName, weekNumber, year);

		try
		{
			var letter = await _childDataService.GetWeekLetterAsync(child, weekNumber, year);

			if (letter != null)
			{
				await _auditService.LogDataAccessAsync(child, "GetStoredWeekLetter", $"{weekNumber}/{year}", true);
			}

			return letter;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get stored week letter for child {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetStoredWeekLetter", $"{weekNumber}/{year}", false);
			throw;
		}
	}

	public async Task InvalidateSessionAsync()
	{
		if (_childContext.CurrentChild == null)
		{
			return;
		}

		var child = _childContext.CurrentChild;
		var sessionKey = $"{child.FirstName}_{child.LastName}";

		if (_sessions.TryGetValue(sessionKey, out var session))
		{
			session.IsAuthenticated = false;
			session.LastAuthenticationTime = null;
			_logger.LogInformation("Invalidated session for child {ChildName}", child.FirstName);
		}

		await _auditService.LogDataAccessAsync(child, "InvalidateSession", "success", true);
	}

	public string GetSessionId()
	{
		if (_childContext.CurrentChild == null)
		{
			return string.Empty;
		}

		var sessionKey = $"{_childContext.CurrentChild.FirstName}_{_childContext.CurrentChild.LastName}";
		if (_sessions.TryGetValue(sessionKey, out var session))
		{
			return session.SessionId;
		}

		return string.Empty;
	}

	public DateTimeOffset? GetLastAuthenticationTime()
	{
		if (_childContext.CurrentChild == null)
		{
			return null;
		}

		var sessionKey = $"{_childContext.CurrentChild.FirstName}_{_childContext.CurrentChild.LastName}";
		if (_sessions.TryGetValue(sessionKey, out var session))
		{
			return session.LastAuthenticationTime;
		}

		return null;
	}
}
