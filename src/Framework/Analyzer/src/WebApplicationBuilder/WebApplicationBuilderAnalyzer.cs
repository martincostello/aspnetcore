// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Analyzers.WebApplicationBuilder;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WebApplicationBuilderAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
    {
        DiagnosticDescriptors.DoNotUseConfigureWebHostWithConfigureHostBuilder,
        DiagnosticDescriptors.DoNotUseConfigureWithConfigureWebHostBuilder,
        DiagnosticDescriptors.DoNotUseUseStartupWithConfigureWebHostBuilder,
    });

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
        {
            var compilation = compilationStartAnalysisContext.Compilation;
            if (!WellKnownTypes.TryCreate(compilation, out var wellKnownTypes))
            {
                Debug.Fail("One or more types could not be found. This usually means you are bad at spelling C# type names.");
                return;
            }

            INamedTypeSymbol[] configureTypes = { wellKnownTypes.WebHostBuilderExtensions };
            INamedTypeSymbol[] configureWebHostTypes = { wellKnownTypes.GenericHostWebHostBuilderExtensions };
            INamedTypeSymbol[] userStartupTypes =
            {
                wellKnownTypes.HostingAbstractionsWebHostBuilderExtensions,
                wellKnownTypes.WebHostBuilderExtensions,
            };

            compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
            {
                var invocation = (IInvocationOperation)operationAnalysisContext.Operation;
                var targetMethod = invocation.TargetMethod;

                // var builder = WebApplication.CreateBuilder();
                // builder.Host.ConfigureWebHost(x => {});
                if (IsDisallowedMethod(
                        operationAnalysisContext,
                        invocation,
                        targetMethod,
                        wellKnownTypes.ConfigureHostBuilder,
                        "ConfigureWebHost",
                        configureWebHostTypes))
                {
                    operationAnalysisContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DoNotUseConfigureWebHostWithConfigureHostBuilder,
                        invocation.Syntax.GetLocation()));
                }

                // var builder = WebApplication.CreateBuilder();
                // builder.WebHost.Configure(x => {});
                if (IsDisallowedMethod(
                        operationAnalysisContext,
                        invocation,
                        targetMethod,
                        wellKnownTypes.ConfigureWebHostBuilder,
                        "Configure",
                        configureTypes))
                {
                    operationAnalysisContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DoNotUseConfigureWithConfigureWebHostBuilder,
                        invocation.Syntax.GetLocation()));
                }

                // var builder = WebApplication.CreateBuilder();
                // builder.WebHost.UseStartup<Startup>();
                if (IsDisallowedMethod(
                        operationAnalysisContext,
                        invocation,
                        targetMethod,
                        wellKnownTypes.ConfigureWebHostBuilder,
                        "UseStartup",
                        userStartupTypes))
                {
                    operationAnalysisContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DoNotUseUseStartupWithConfigureWebHostBuilder,
                        invocation.Syntax.GetLocation()));
                }

            }, OperationKind.Invocation);
        });
    }

    private static bool IsDisallowedMethod(
        in OperationAnalysisContext context,
        IInvocationOperation invocation,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol disallowedReceiverType,
        string disallowedMethodName,
        INamedTypeSymbol[] disallowedMethodTypes)
    {
        if (!IsDisallowedMethod(methodSymbol, disallowedMethodName, disallowedMethodTypes))
        {
            return false;
        }

        var receiverType = GetReceiverType(invocation, context.CancellationToken);

        if (!SymbolEqualityComparer.Default.Equals(receiverType, disallowedReceiverType))
        {
            return false;
        }

        return true;

        static bool IsDisallowedMethod(
            IMethodSymbol methodSymbol,
            string disallowedMethodName,
            INamedTypeSymbol[] disallowedMethodTypes)
        {
            if (!string.Equals(methodSymbol?.Name, disallowedMethodName, StringComparison.Ordinal))
            {
                return false;
            }

            return disallowedMethodTypes.Any(type => SymbolEqualityComparer.Default.Equals(type, methodSymbol.ContainingType));
        }
    }

    // GetReceiverType() adapted from IOperationExtensions.GetReceiverType in dotnet/roslyn-analyzers.
    // See https://github.com/dotnet/roslyn-analyzers/blob/762b08948cdcc1d94352fba681296be7bf474dd7/src/Utilities/Compiler/Extensions/IOperationExtensions.cs#L22-L51

    private static INamedTypeSymbol? GetReceiverType(
        IInvocationOperation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.Instance != null)
        {
            return GetReceiverType(invocation.Instance.Syntax, invocation.SemanticModel, cancellationToken);
        }
        else if (invocation.TargetMethod.IsExtensionMethod && !invocation.TargetMethod.Parameters.IsEmpty)
        {
            var firstArg = invocation.Arguments.FirstOrDefault();
            if (firstArg != null)
            {
                return GetReceiverType(firstArg.Value.Syntax, invocation.SemanticModel, cancellationToken);
            }
            else if (invocation.TargetMethod.Parameters[0].IsParams)
            {
                return invocation.TargetMethod.Parameters[0].Type as INamedTypeSymbol;
            }
        }

        static INamedTypeSymbol? GetReceiverType(
            SyntaxNode receiverSyntax,
            SemanticModel model,
            CancellationToken cancellationToken)
        {
            var typeInfo = model.GetTypeInfo(receiverSyntax, cancellationToken);
            return typeInfo.Type as INamedTypeSymbol;
        }

        return null;
    }
}
