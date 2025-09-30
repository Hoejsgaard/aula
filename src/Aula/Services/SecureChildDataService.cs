using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Services;

/// <summary>
/// Secure child-aware data service with defense-in-depth security layers.
/// Provides isolated data operations for each child with comprehensive security controls.
/// </summary>
public class SecureChildDataService : IChildDataService
{
	private readonly IChildContext _context;
	private readonly IChildContextValidator _contextValidator;
	private readonly IChildAuditService _auditService;
	private readonly IChildRateLimiter _rateLimiter;
	private readonly IDataService _dataService;
	private readonly ISupabaseService _supabaseService;
	private readonly IMinUddannelseClient _minUddannelseClient;
	private readonly ILogger<SecureChildDataService> _logger;

	public SecureChildDataService(
		IChildContext context,
		IChildContextValidator contextValidator,
		IChildAuditService auditService,
		IChildRateLimiter rateLimiter,
		IDataService dataService,
		ISupabaseService supabaseService,
		IMinUddannelseClient minUddannelseClient,
		ILogger<SecureChildDataService> logger)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
		_auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
		_rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
		_dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
		_supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
		_minUddannelseClient = minUddannelseClient ?? throw new ArgumentNullException(nameof(minUddannelseClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task CacheWeekLetterAsync(int weekNumber, int year, JObject weekLetter)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "write:week_letter"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to cache week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "write:week_letter", SecuritySeverity.Warning);
			throw new UnauthorizedAccessException($"Child {child.FirstName} does not have permission to cache week letters");
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "CacheWeekLetter"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} caching week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "CacheWeekLetter", SecuritySeverity.Warning);
			throw new RateLimitExceededException("CacheWeekLetter", child.FirstName, 100, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging (pre-operation)
			_logger.LogInformation("Caching week letter for {ChildName} week {WeekNumber}/{Year}",
				child.FirstName, weekNumber, year);

			// Layer 5: Secure operation with child-prefixed caching
			_dataService.CacheWeekLetter(child, weekNumber, year, weekLetter);

			// Record successful operation
			await _rateLimiter.RecordOperationAsync(child, "CacheWeekLetter");
			await _auditService.LogDataAccessAsync(child, "CacheWeekLetter", $"week_{weekNumber}_{year}", true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to cache week letter for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "CacheWeekLetter", $"week_{weekNumber}_{year}", false);
			throw;
		}
	}

	public async Task<JObject?> GetWeekLetterAsync(int weekNumber, int year)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_letter"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to read week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning);
			return null;
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "GetWeekLetter"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} reading week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "GetWeekLetter", SecuritySeverity.Warning);
			throw new RateLimitExceededException("GetWeekLetter", child.FirstName, 100, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging (pre-operation)
			_logger.LogInformation("Retrieving week letter for {ChildName} week {WeekNumber}/{Year}",
				child.FirstName, weekNumber, year);

			// Layer 5: Secure operation with child-prefixed caching
			var result = _dataService.GetWeekLetter(child, weekNumber, year);

			// Record successful operation
			await _rateLimiter.RecordOperationAsync(child, "GetWeekLetter");
			await _auditService.LogDataAccessAsync(child, "GetWeekLetter", $"week_{weekNumber}_{year}", result != null);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve week letter for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetWeekLetter", $"week_{weekNumber}_{year}", false);
			throw;
		}
	}

	public async Task CacheWeekScheduleAsync(int weekNumber, int year, JObject weekSchedule)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "write:week_schedule"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to cache week schedule", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "write:week_schedule", SecuritySeverity.Warning);
			throw new UnauthorizedAccessException($"Child {child.FirstName} does not have permission to cache week schedules");
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "CacheWeekSchedule"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} caching week schedule", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "CacheWeekSchedule", SecuritySeverity.Warning);
			throw new RateLimitExceededException("CacheWeekSchedule", child.FirstName, 100, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging
			_logger.LogInformation("Caching week schedule for {ChildName} week {WeekNumber}/{Year}",
				child.FirstName, weekNumber, year);

			// Layer 5: Secure operation
			_dataService.CacheWeekSchedule(child, weekNumber, year, weekSchedule);

			await _rateLimiter.RecordOperationAsync(child, "CacheWeekSchedule");
			await _auditService.LogDataAccessAsync(child, "CacheWeekSchedule", $"schedule_{weekNumber}_{year}", true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to cache week schedule for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "CacheWeekSchedule", $"schedule_{weekNumber}_{year}", false);
			throw;
		}
	}

	public async Task<JObject?> GetWeekScheduleAsync(int weekNumber, int year)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_schedule"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to read week schedule", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_schedule", SecuritySeverity.Warning);
			return null;
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "GetWeekSchedule"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} reading week schedule", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "GetWeekSchedule", SecuritySeverity.Warning);
			throw new RateLimitExceededException("GetWeekSchedule", child.FirstName, 100, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging
			_logger.LogInformation("Retrieving week schedule for {ChildName} week {WeekNumber}/{Year}",
				child.FirstName, weekNumber, year);

			// Layer 5: Secure operation
			var result = _dataService.GetWeekSchedule(child, weekNumber, year);

			await _rateLimiter.RecordOperationAsync(child, "GetWeekSchedule");
			await _auditService.LogDataAccessAsync(child, "GetWeekSchedule", $"schedule_{weekNumber}_{year}", result != null);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve week schedule for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetWeekSchedule", $"schedule_{weekNumber}_{year}", false);
			throw;
		}
	}

	public async Task<bool> StoreWeekLetterAsync(int weekNumber, int year, JObject weekLetter)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "write:database"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to store week letter in database", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "write:database", SecuritySeverity.Warning);
			return false;
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "StoreWeekLetter"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} storing week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "StoreWeekLetter", SecuritySeverity.Warning);
			throw new RateLimitExceededException("StoreWeekLetter", child.FirstName, 10, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging
			_logger.LogInformation("Storing week letter for {ChildName} week {WeekNumber}/{Year} in database",
				child.FirstName, weekNumber, year);

			// Layer 5: Secure database operation (parameterized query in SupabaseService)
			var contentHash = ComputeHash(weekLetter.ToString());
			await _supabaseService.StoreWeekLetterAsync(
				child.FirstName,
				weekNumber,
				year,
				contentHash,
				weekLetter.ToString());

			await _rateLimiter.RecordOperationAsync(child, "StoreWeekLetter");
			await _auditService.LogDataAccessAsync(child, "StoreWeekLetter", $"db_week_{weekNumber}_{year}", true);

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to store week letter for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "StoreWeekLetter", $"db_week_{weekNumber}_{year}", false);
			return false;
		}
	}

	public async Task<bool> DeleteWeekLetterAsync(int weekNumber, int year)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "delete:database"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to delete week letter from database", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "delete:database", SecuritySeverity.Critical);
			return false;
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "DeleteWeekLetter"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} deleting week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "DeleteWeekLetter", SecuritySeverity.Warning);
			throw new RateLimitExceededException("DeleteWeekLetter", child.FirstName, 5, TimeSpan.FromMinutes(10));
		}

		try
		{
			// Layer 4: Audit logging (critical operation)
			_logger.LogWarning("Deleting week letter for {ChildName} week {WeekNumber}/{Year} from database",
				child.FirstName, weekNumber, year);
			await _auditService.LogSecurityEventAsync(child, "DataDeletion", $"week_{weekNumber}_{year}", SecuritySeverity.Warning);

			// Layer 5: Secure database operation
			await _supabaseService.DeleteWeekLetterAsync(child.FirstName, weekNumber, year);

			await _rateLimiter.RecordOperationAsync(child, "DeleteWeekLetter");
			await _auditService.LogDataAccessAsync(child, "DeleteWeekLetter", $"db_week_{weekNumber}_{year}", true);

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete week letter for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "DeleteWeekLetter", $"db_week_{weekNumber}_{year}", false);
			return false;
		}
	}

	public async Task<List<JObject>> GetStoredWeekLettersAsync(int? year = null)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:database"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to read stored week letters", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:database", SecuritySeverity.Warning);
			return new List<JObject>();
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "GetStoredWeekLetters"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} reading stored week letters", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "GetStoredWeekLetters", SecuritySeverity.Warning);
			throw new RateLimitExceededException("GetStoredWeekLetters", child.FirstName, 20, TimeSpan.FromMinutes(1));
		}

		try
		{
			// Layer 4: Audit logging
			_logger.LogInformation("Retrieving stored week letters for {ChildName} year {Year}",
				child.FirstName, year?.ToString() ?? "all");

			// Layer 5: Secure database operation (filtered by child)
			var letters = await _supabaseService.GetStoredWeekLettersAsync(child.FirstName, year);

			var results = new List<JObject>();
			foreach (var letter in letters)
			{
				if (!string.IsNullOrEmpty(letter.RawContent))
				{
					try
					{
						var json = JObject.Parse(letter.RawContent);
						results.Add(json);
					}
					catch (Exception parseEx)
					{
						_logger.LogWarning(parseEx, "Failed to parse stored week letter for {ChildName}", child.FirstName);
					}
				}
			}

			await _rateLimiter.RecordOperationAsync(child, "GetStoredWeekLetters");
			await _auditService.LogDataAccessAsync(child, "GetStoredWeekLetters", $"db_year_{year?.ToString() ?? "all"}", true);

			return results;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve stored week letters for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetStoredWeekLetters", $"db_year_{year?.ToString() ?? "all"}", false);
			return new List<JObject>();
		}
	}

	public async Task<JObject?> GetOrFetchWeekLetterAsync(DateOnly date, bool allowLiveFetch = false)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Calculate week number for the given date
		var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
		var weekNumber = calendar.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue),
			System.Globalization.CalendarWeekRule.FirstFourDayWeek,
			DayOfWeek.Monday);
		var year = date.Year;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_letter"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to get or fetch week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning);
			return null;
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "GetOrFetchWeekLetter"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} getting week letter", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "GetOrFetchWeekLetter", SecuritySeverity.Warning);
			throw new RateLimitExceededException("GetOrFetchWeekLetter", child.FirstName, 50, TimeSpan.FromMinutes(1));
		}

		try
		{
			_logger.LogInformation("Getting or fetching week letter for {ChildName} date {Date} (week {WeekNumber}/{Year})",
				child.FirstName, date, weekNumber, year);

			// Check cache first
			var cached = _dataService.GetWeekLetter(child, weekNumber, year);
			if (cached != null)
			{
				_logger.LogDebug("Week letter found in cache for {ChildName}", child.FirstName);
				return cached;
			}

			// Check database
			var storedContent = await _supabaseService.GetStoredWeekLetterAsync(child.FirstName, weekNumber, year);
			if (!string.IsNullOrEmpty(storedContent))
			{
				try
				{
					var json = JObject.Parse(storedContent);
					// Cache it for future use
					_dataService.CacheWeekLetter(child, weekNumber, year, json);
					_logger.LogDebug("Week letter found in database for {ChildName}", child.FirstName);
					return json;
				}
				catch (Exception parseEx)
				{
					_logger.LogWarning(parseEx, "Failed to parse stored week letter for {ChildName}", child.FirstName);
				}
			}

			// Fetch from MinUddannelse if allowed
			if (allowLiveFetch)
			{
				_logger.LogInformation("Fetching week letter from MinUddannelse for {ChildName}", child.FirstName);

				try
				{
					// Use the MinUddannelse client directly to fetch the week letter
					var fetchedLetter = await _minUddannelseClient.GetWeekLetter(child, date, true);

					if (fetchedLetter != null)
					{
						_logger.LogInformation("Successfully fetched week letter for {ChildName} week {WeekNumber}/{Year}",
							child.FirstName, weekNumber, year);

						// Cache the fetched letter for future use
						await CacheWeekLetterAsync(weekNumber, year, fetchedLetter);

						await _rateLimiter.RecordOperationAsync(child, "GetOrFetchWeekLetter");
						await _auditService.LogDataAccessAsync(child, "GetOrFetchWeekLetter", $"letter_{weekNumber}_{year}", true);

						return fetchedLetter;
					}
				}
				catch (Exception fetchEx)
				{
					_logger.LogError(fetchEx, "Failed to fetch week letter from MinUddannelse for {ChildName}", child.FirstName);
				}
			}

			await _rateLimiter.RecordOperationAsync(child, "GetOrFetchWeekLetter");
			await _auditService.LogDataAccessAsync(child, "GetOrFetchWeekLetter", $"letter_{weekNumber}_{year}", false);

			return null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get or fetch week letter for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetOrFetchWeekLetter", $"letter_{weekNumber}_{year}", false);
			throw;
		}
	}

	public async Task<List<JObject>> GetAllWeekLettersAsync()
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:all_week_letters"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to get all week letters", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:all_week_letters", SecuritySeverity.Warning);
			return new List<JObject>();
		}

		// Layer 3: Rate limiting
		if (!await _rateLimiter.IsAllowedAsync(child, "GetAllWeekLetters"))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} getting all week letters", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "GetAllWeekLetters", SecuritySeverity.Warning);
			throw new RateLimitExceededException("GetAllWeekLetters", child.FirstName, 10, TimeSpan.FromMinutes(5));
		}

		try
		{
			_logger.LogInformation("Getting all week letters for {ChildName}", child.FirstName);

			// Get all stored week letters from database
			var letters = await GetStoredWeekLettersAsync();

			await _rateLimiter.RecordOperationAsync(child, "GetAllWeekLetters");
			await _auditService.LogDataAccessAsync(child, "GetAllWeekLetters", "all_letters", true);

			return letters;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get all week letters for {ChildName}", child.FirstName);
			await _auditService.LogDataAccessAsync(child, "GetAllWeekLetters", "all_letters", false);
			return new List<JObject>();
		}
	}

	private static string ComputeHash(string content)
	{
		using var sha256 = System.Security.Cryptography.SHA256.Create();
		var bytes = System.Text.Encoding.UTF8.GetBytes(content);
		var hash = sha256.ComputeHash(bytes);
		return Convert.ToBase64String(hash);
	}
}
