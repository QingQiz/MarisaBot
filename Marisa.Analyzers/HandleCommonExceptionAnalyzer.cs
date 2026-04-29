using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marisa.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandleCommonExceptionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MARISA001";

    private const string HandleCommonExceptionMetadataName = "Marisa.Plugin.Shared.Interface.IHandleCommonException";
    private const string MessageMetadataName = "Marisa.BotDriver.Entity.Message.Message";
    private const string ExceptionMetadataName = "System.Exception";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "IHandleCommonException requires ExceptionHandler override",
        messageFormat: "Class '{0}' implements IHandleCommonException but does not override ExceptionHandler(Exception, Message)",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Classes that directly implement IHandleCommonException should override ExceptionHandler(Exception, Message) to wire the common exception handler into the plugin pipeline.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var handleCommonExceptionInterface = context.Compilation.GetTypeByMetadataName(HandleCommonExceptionMetadataName);
        if (handleCommonExceptionInterface is null)
        {
            return;
        }

        if (!type.Interfaces.Contains(handleCommonExceptionInterface, SymbolEqualityComparer.Default))
        {
            return;
        }

        if (DeclaresExceptionHandlerOverride(type, context.Compilation))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }

    private static bool DeclaresExceptionHandlerOverride(INamedTypeSymbol type, Compilation compilation)
    {
        var messageType = compilation.GetTypeByMetadataName(MessageMetadataName);
        var exceptionType = compilation.GetTypeByMetadataName(ExceptionMetadataName);

        return type.GetMembers("ExceptionHandler")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.IsOverride &&
                method.ReturnType.Name == nameof(Task) &&
                method.Parameters.Length == 2 &&
                (exceptionType is null || SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, exceptionType)) &&
                (messageType is null || SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, messageType)));
    }
}