using System;
using System.Threading.Tasks;
using Aula.Context;
using Microsoft.Extensions.Logging;

namespace Aula.Services;

/// <summary>
/// Child-aware OpenAI service that ensures all AI operations happen within child context.
/// This is a simplified implementation for Chapter 7 migration.
/// </summary>
public class SecureChildAwareOpenAiService : IChildAwareOpenAiService
{
	private readonly IChildContext _childContext;
	private readonly IOpenAiService _openAiService;
	private readonly ILogger<SecureChildAwareOpenAiService> _logger;

	public SecureChildAwareOpenAiService(
		IChildContext childContext,
		IOpenAiService openAiService,
		ILogger<SecureChildAwareOpenAiService> logger)
	{
		_childContext = childContext ?? throw new ArgumentNullException(nameof(childContext));
		_openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<string?> GetResponseAsync(string query)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot get AI response without child context");
		}

		_logger.LogInformation("Getting AI response for child {ChildName}",
			_childContext.CurrentChild.FirstName);

		// Add child context to the query
		var contextualQuery = $"[Context: Child {_childContext.CurrentChild.FirstName}] {query}";

		// Use the ProcessQueryWithToolsAsync method which exists in IOpenAiService
		return await _openAiService.ProcessQueryWithToolsAsync(contextualQuery,
			$"child_{_childContext.CurrentChild.FirstName}",
			ChatInterface.Slack);
	}

	public async Task<string?> GetResponseWithContextAsync(string query, string conversationId)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot get AI response without child context");
		}

		_logger.LogInformation("Getting AI response with context for child {ChildName}, conversation {ConversationId}",
			_childContext.CurrentChild.FirstName, conversationId);

		// Create child-specific conversation ID
		var childConversationId = $"{_childContext.CurrentChild.FirstName}_{conversationId}";

		return await _openAiService.ProcessQueryWithToolsAsync(query,
			childConversationId,
			ChatInterface.Slack);
	}

	public async Task ClearConversationHistoryAsync(string conversationId)
	{
		if (_childContext.CurrentChild == null)
		{
			throw new InvalidOperationException("Cannot clear conversation without child context");
		}

		_logger.LogInformation("Clearing conversation history for child {ChildName}, conversation {ConversationId}",
			_childContext.CurrentChild.FirstName, conversationId);

		// Create child-specific conversation ID
		var childConversationId = $"{_childContext.CurrentChild.FirstName}_{conversationId}";

		// Use the ClearConversationHistory method from IOpenAiService
		_openAiService.ClearConversationHistory(childConversationId);
		await Task.CompletedTask;
	}
}
