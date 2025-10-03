# Create .NET Microservice PRP

## Feature file: $ARGUMENTS

Generate a complete PRP for .NET 9+ microservice feature implementation with thorough research. Ensure context is passed to the AI agent to enable self-validation and iterative refinement. Read the feature file first to understand what needs to be created, how the examples provided help, and any other considerations.

The AI agent only gets the context you are appending to the PRP and training data. Assume the AI agent has access to the codebase and the same knowledge cutoff as you, so it's important that your research findings are included or referenced in the PRP. The Agent has Websearch capabilities, so pass URLs to documentation and examples.

## Research Process

1. **Codebase Analysis**
   - Search for similar microservice features/patterns in the codebase
   - Identify .NET files to reference in PRP (Controllers, Services, Entities)
   - Note existing Clean Architecture conventions to follow
   - Check xUnit test patterns for validation approach
   - Review Entity Framework configurations and migrations

2. **External Research**
   - Search for similar .NET microservice features/patterns online
   - .NET 9 documentation (include specific URLs)
   - MediatR, Entity Framework, FluentValidation examples
   - Azure Service Bus integration patterns
   - EU VAT compliance and financial precision best practices
   - Implementation examples (GitHub/StackOverflow/Microsoft docs)

3. **User Clarification** (if needed)
   - Specific microservice patterns to mirror and where to find them?
   - Integration requirements with other services and where to find them?
   - Multi-tenant considerations and database design?

## PRP Generation

Using PRPs/templates/prp_base.md as template:

### Critical Context to Include and pass to the AI agent as part of the PRP
- **Documentation**: URLs with specific sections (.NET docs, NuGet packages)
- **Code Examples**: Real C# snippets from codebase
- **Gotchas**: .NET quirks, Entity Framework migrations, Service Bus patterns
- **Patterns**: Existing microservice approaches to follow
- **Financial Requirements**: Decimal precision, EU VAT compliance, audit trails

### Implementation Blueprint
- Start with Clean Architecture approach
- Reference real .NET microservice files for patterns
- Include MediatR CQRS implementation strategy
- Include Entity Framework configuration and migrations
- Include xUnit testing with FluentAssertions and FsCheck
- Include Kubernetes deployment considerations
- List tasks to be completed to fulfill the PRP in the order they should be completed

### Validation Gates (Must be Executable) for .NET
```bash
# Build and compile
dotnet build --configuration Release

# Code formatting and analysis
dotnet format --verify-no-changes
dotnet run --project StyleCop.Analyzers

# Unit Tests
dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"

# Integration Tests
dotnet test --configuration Release --filter Category=Integration

```

*** CRITICAL AFTER YOU ARE DONE RESEARCHING AND EXPLORING THE CODEBASE BEFORE YOU START WRITING THE PRP ***

*** ULTRATHINK ABOUT THE PRP AND PLAN YOUR APPROACH THEN START WRITING THE PRP ***

## Output
Save as: `PRPs/{feature-name}.md`

**After PRP Creation**: If this PRP was based on a seed from `PRPs/seeds/`, move that seed to `PRPs/done/seeds/` to indicate it has been used.

## Quality Checklist
- [ ] All necessary .NET context included
- [ ] Validation gates are executable by AI
- [ ] References existing microservice patterns
- [ ] Clear Clean Architecture implementation path
- [ ] Error handling and Result patterns documented
- [ ] Financial precision and EU VAT compliance addressed
- [ ] Multi-tenant considerations included

Score the PRP on a scale of 1-10 (confidence level to succeed in one-pass implementation using claude codes)

Remember: The goal is one-pass implementation success through comprehensive .NET microservice context.