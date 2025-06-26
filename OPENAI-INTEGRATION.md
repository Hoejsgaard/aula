# OpenAI Integration for Aula

This integration adds AI capabilities to the Aula project, allowing you to:

1. Summarize week letters from schools
2. Ask questions about week letters
3. Extract key information from week letters (events, deadlines, etc.)

## Configuration

To use the OpenAI integration, you need to add your API key to the `appsettings.json` file:

```json
{
  "OpenAi": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4" 
  }
}
```

You can obtain an API key from the [OpenAI platform](https://platform.openai.com/account/api-keys).

## Features

### Summarizing Week Letters

The integration automatically summarizes week letters when they are fetched, providing a concise overview of the most important information. These summaries are posted to Slack alongside the full week letter.

### Extracting Key Information

The integration can extract structured information from week letters, such as:

- Important dates and deadlines
- Required materials for upcoming activities
- Special events or field trips
- Homework assignments

This information is formatted as JSON and posted to Slack for easy reference.

### Asking Questions

Through the `IAgentService` interface, you can programmatically ask questions about week letters:

```csharp
// Example usage
var answer = await agentService.AskQuestionAboutWeekLetterAsync(
    child, 
    DateOnly.FromDateTime(DateTime.Today), 
    "What activities are planned for next week?"
);
```

## Implementation Details

The integration uses the OpenAI API with the following components:

- `IOpenAiService`: Interface for OpenAI operations
- `OpenAiService`: Implementation of the interface
- Updated `AgentService` with AI-powered methods

The default model is GPT-4, but it will fall back to GPT-3.5 Turbo if GPT-4 is not available for your account.

## Error Handling

The integration includes robust error handling to ensure that failures in AI processing don't affect the core functionality of retrieving and displaying week letters.

## Testing

The project includes tests for the OpenAI integration. These tests are skipped by default since they require a valid OpenAI API key. To run them, remove the `Skip` attribute from the test methods in `OpenAiServiceTests.cs` and provide a valid API key.

## What's Included

1. **OpenAI Integration**:
   - `IOpenAiService` interface
   - `OpenAiService` implementation
   - Configuration in `Config.cs`

2. **AgentService Extensions**:
   - `SummarizeWeekLetterAsync`
   - `AskQuestionAboutWeekLetterAsync`
   - `ExtractKeyInformationFromWeekLetterAsync`

3. **Tests**:
   - Unit tests for all new functionality

4. **Example Usage**:
   - Automatic summarization in `Program.cs`
   - Key information extraction in `Program.cs` 