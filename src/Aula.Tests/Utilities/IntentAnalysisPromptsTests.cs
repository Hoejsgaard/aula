using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class IntentAnalysisPromptsTests
{
    [Fact]
    public void AnalysisTemplate_IsNotEmpty()
    {
        // Assert
        Assert.NotNull(IntentAnalysisPrompts.AnalysisTemplate);
        Assert.NotEmpty(IntentAnalysisPrompts.AnalysisTemplate);
    }

    [Fact]
    public void AnalysisTemplate_ContainsRequiredPlaceholders()
    {
        // Assert
        Assert.Contains("{0}", IntentAnalysisPrompts.AnalysisTemplate); // Query placeholder
        Assert.Contains("{1}", IntentAnalysisPrompts.AnalysisTemplate); // Examples placeholder
    }

    [Fact]
    public void AnalysisTemplate_ContainsAllToolActions()
    {
        var template = IntentAnalysisPrompts.AnalysisTemplate;

        // Assert - All tool actions should be documented in the template
        Assert.Contains("CREATE_REMINDER", template);
        Assert.Contains("LIST_REMINDERS", template);
        Assert.Contains("DELETE_REMINDER", template);
        Assert.Contains("GET_CURRENT_TIME", template);
        Assert.Contains("HELP", template);
        Assert.Contains("INFORMATION_QUERY", template);
        Assert.Contains("TOOL_CALL", template);
    }

    [Fact]
    public void ToolExamples_ContainsExpectedEntries()
    {
        var examples = IntentAnalysisPrompts.ToolExamples;

        // Assert
        Assert.NotNull(examples);
        Assert.NotEmpty(examples);
        Assert.True(examples.Count >= 10); // Should have multiple examples
    }

    [Theory]
    [InlineData("CREATE_REMINDER")]
    [InlineData("CREATE_REMINDER_DANISH")]
    [InlineData("LIST_REMINDERS")]
    [InlineData("LIST_REMINDERS_DANISH")]
    [InlineData("DELETE_REMINDER")]
    [InlineData("GET_CURRENT_TIME")]
    [InlineData("GET_CURRENT_TIME_DANISH")]
    [InlineData("INFORMATION_QUERY")]
    [InlineData("INFORMATION_QUERY_DANISH")]
    [InlineData("WEEK_LETTER_QUERY")]
    public void ToolExamples_ContainsRequiredKeys(string key)
    {
        // Assert
        Assert.True(IntentAnalysisPrompts.ToolExamples.ContainsKey(key));
        Assert.NotEmpty(IntentAnalysisPrompts.ToolExamples[key]);
    }

    [Fact]
    public void ToolExamples_AllEntriesFollowExpectedFormat()
    {
        foreach (var example in IntentAnalysisPrompts.ToolExamples.Values)
        {
            // Assert - Each example should contain a query and arrow
            Assert.Contains("\"", example); // Should contain quoted query
            Assert.Contains("→", example); // Should contain arrow
            
            // Should end with either TOOL_CALL: ACTION or INFORMATION_QUERY
            Assert.True(example.EndsWith("INFORMATION_QUERY") || 
                       example.Contains("TOOL_CALL:"),
                       $"Example doesn't follow expected format: {example}");
        }
    }

    [Fact]
    public void ToolExamples_ContainsBothLanguages()
    {
        var examples = IntentAnalysisPrompts.ToolExamples;
        
        // Count English vs Danish examples
        var danishKeys = examples.Keys.Where(k => k.EndsWith("_DANISH")).ToList();
        var englishKeys = examples.Keys.Where(k => !k.EndsWith("_DANISH")).ToList();

        // Assert - Should have examples in both languages
        Assert.NotEmpty(danishKeys);
        Assert.NotEmpty(englishKeys);
    }

    [Theory]
    [InlineData("Remind me about soccer")]
    [InlineData("What does Emma have today?")]
    [InlineData("Help me")]
    [InlineData("")]
    public void GetFormattedPrompt_WithVariousQueries_ReturnsValidPrompt(string query)
    {
        // Act
        var result = IntentAnalysisPrompts.GetFormattedPrompt(query);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(query, result); // Query should be embedded in prompt
        Assert.Contains("Examples:", result); // Examples should be included
        Assert.DoesNotContain("{0}", result); // Placeholder should be replaced
        Assert.DoesNotContain("{1}", result); // Placeholder should be replaced
    }

    [Fact]
    public void GetFormattedPrompt_IncludesAllExamples()
    {
        // Arrange
        var testQuery = "test query";

        // Act
        var result = IntentAnalysisPrompts.GetFormattedPrompt(testQuery);

        // Assert - All examples should be included in the formatted prompt
        foreach (var example in IntentAnalysisPrompts.ToolExamples.Values)
        {
            Assert.Contains(example, result);
        }
    }

    [Theory]
    [InlineData("CREATE_REMINDER", "remind")]
    [InlineData("LIST_REMINDERS", "reminders")]
    [InlineData("DELETE_REMINDER", "delete")]
    [InlineData("GET_CURRENT_TIME", "time")]
    [InlineData("INFORMATION_QUERY", "what")]
    public void ToolExamples_CoverExpectedUseCase(string expectedKey, string queryPattern)
    {
        // Find examples that match the expected use case
        var matchingExamples = IntentAnalysisPrompts.ToolExamples
            .Where(kvp => kvp.Key.StartsWith(expectedKey.Replace("_DANISH", "")))
            .ToList();

        // Assert - Should have at least one example for this use case
        Assert.NotEmpty(matchingExamples);
        
        // At least one example should contain similar wording
        var hasRelevantExample = matchingExamples.Any(ex => 
            ex.Value.ToLower().Contains(queryPattern.ToLower()));
        
        Assert.True(hasRelevantExample, 
            $"No relevant example found for {expectedKey} with pattern '{queryPattern}'. Examples: {string.Join(", ", matchingExamples.Select(e => e.Value))}");
    }

    [Fact]
    public void GetFormattedPrompt_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var queryWithSpecialChars = "Remind me about \"TestChild1's soccer\" at 8:00 PM";

        // Act
        var result = IntentAnalysisPrompts.GetFormattedPrompt(queryWithSpecialChars);

        // Assert
        Assert.Contains(queryWithSpecialChars, result);
        Assert.DoesNotContain("{{", result); // No double braces from formatting errors
        Assert.DoesNotContain("}}", result);
    }

    [Fact]
    public void ToolExamples_ToolCallExamples_FollowCorrectFormat()
    {
        var toolCallExamples = IntentAnalysisPrompts.ToolExamples.Values
            .Where(ex => ex.Contains("TOOL_CALL:"))
            .ToList();

        foreach (var example in toolCallExamples)
        {
            // Assert - TOOL_CALL examples should have action name after colon
            var toolCallPart = example.Split("TOOL_CALL:")[1].Trim();
            Assert.NotEmpty(toolCallPart);
            Assert.DoesNotContain(" ", toolCallPart); // Action should be single word/underscore
            Assert.True(char.IsUpper(toolCallPart[0])); // Should start with uppercase
        }
    }

    [Fact]
    public void AnalysisTemplate_ContainsInstructionsForBothResponseTypes()
    {
        var template = IntentAnalysisPrompts.AnalysisTemplate;

        // Assert - Template should explain both response types
        Assert.Contains("TOOL_CALL:", template);
        Assert.Contains("INFORMATION_QUERY", template);
        Assert.Contains("respond with:", template.ToLower());
    }
}