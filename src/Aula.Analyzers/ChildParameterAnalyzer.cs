using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aula.Analyzers;

/// <summary>
/// Roslyn analyzer to enforce child-centric architecture rules.
/// Prevents the use of Child parameters in service interfaces.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ChildParameterAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "ARCH001";
	private const string Category = "Architecture";

	private static readonly string[] AllowedTypes =
	{
		"IChildServiceCoordinator",
		"ChildServiceCoordinator",
		"IChildContext",
		"ChildContext",
		"IChildContextValidator",
		"ChildContextValidator",
		"IChildAuditService",
		"ChildAuditService"
	};

	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Avoid Child parameters in service interfaces",
		"Method '{0}' in '{1}' should not have Child parameters. Use IChildContext for child-aware services.",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Service methods should not accept Child parameters directly. Use IChildContext for proper isolation.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;
		var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

		if (methodSymbol == null)
			return;

		// Check if the containing type is in the allowed list
		var containingType = methodSymbol.ContainingType;
		if (containingType != null && AllowedTypes.Contains(containingType.Name))
			return;

		// Check if this is in a service namespace
		var namespaceName = containingType?.ContainingNamespace?.ToDisplayString();
		if (namespaceName == null ||
			(!namespaceName.Contains("Services") &&
			 !namespaceName.Contains("Integration") &&
			 !namespaceName.Contains("Channels")))
			return;

		// Check for Child parameters
		foreach (var parameter in methodSymbol.Parameters)
		{
			if (parameter.Type.Name == "Child")
			{
				var diagnostic = Diagnostic.Create(
					Rule,
					parameter.Locations[0],
					methodSymbol.Name,
					containingType.Name);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	private static void AnalyzeInterface(SyntaxNodeAnalysisContext context)
	{
		var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
		var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration);

		if (interfaceSymbol == null)
			return;

		// Check if this interface is in the allowed list
		if (AllowedTypes.Contains(interfaceSymbol.Name))
			return;

		// Check all members for Child parameters
		foreach (var member in interfaceSymbol.GetMembers())
		{
			if (member is IMethodSymbol method)
			{
				foreach (var parameter in method.Parameters)
				{
					if (parameter.Type.Name == "Child")
					{
						var diagnostic = Diagnostic.Create(
							Rule,
							parameter.Locations[0],
							method.Name,
							interfaceSymbol.Name);

						context.ReportDiagnostic(diagnostic);
					}
				}
			}
		}
	}
}

/// <summary>
/// Analyzer to detect usage of obsolete interfaces.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ObsoleteInterfaceAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "ARCH003";
	private const string Category = "Architecture";

	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Avoid using obsolete interfaces",
		"Interface '{0}' is obsolete. Migrate to child-aware services.",
		Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Legacy interfaces with Child parameters are deprecated. Use child-aware services instead.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
	}

	private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
	{
		var identifier = (IdentifierNameSyntax)context.Node;
		var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;

		if (symbol == null)
			return;

		// Check if the symbol has the Obsolete attribute
		var attributes = symbol.GetAttributes();
		var hasObsoleteAttribute = attributes.Any(a =>
			a.AttributeClass?.Name == "ObsoleteAttribute" ||
			a.AttributeClass?.Name == "Obsolete");

		if (hasObsoleteAttribute)
		{
			var diagnostic = Diagnostic.Create(
				Rule,
				identifier.GetLocation(),
				symbol.Name);

			context.ReportDiagnostic(diagnostic);
		}
	}
}

/// <summary>
/// Analyzer to ensure child-aware services inject IChildContext.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ChildContextInjectionAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "ARCH004";
	private const string Category = "Architecture";

	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Child-aware services must inject IChildContext",
		"Service '{0}' appears to be child-aware but does not inject IChildContext",
		Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Services with 'Secure' prefix or containing 'Child' in the name must inject IChildContext for proper isolation.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
	}

	private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

		if (classSymbol == null || classSymbol.IsAbstract)
			return;

		// Check if this is a child-aware service
		var className = classSymbol.Name;
		if (!className.StartsWith("Secure") && !className.Contains("Child"))
			return;

		// Skip test classes
		if (className.EndsWith("Tests") || className.EndsWith("Test"))
			return;

		// Check constructors for IChildContext parameter
		var constructors = classSymbol.Constructors;
		var hasChildContext = false;

		foreach (var constructor in constructors)
		{
			if (constructor.Parameters.Any(p => p.Type.Name == "IChildContext"))
			{
				hasChildContext = true;
				break;
			}
		}

		if (!hasChildContext && constructors.Any(c => !c.IsImplicitlyDeclared))
		{
			var diagnostic = Diagnostic.Create(
				Rule,
				classDeclaration.Identifier.GetLocation(),
				className);

			context.ReportDiagnostic(diagnostic);
		}
	}
}