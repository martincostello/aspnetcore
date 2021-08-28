// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Analyzers.WebApplicationBuilder;

public partial class WebApplicationBuilderAnalyzer : DiagnosticAnalyzer
{
    private static void DisallowConfigureHostBuilderConfigureWebHost(
        in OperationAnalysisContext context,
        WellKnownTypes wellKnownTypes,
        IInvocationOperation invocation,
        IMethodSymbol methodSymbol)
    {
        if (IsDisallowedMethod(
                context,
                invocation,
                methodSymbol,
                wellKnownTypes.ConfigureHostBuilder,
                "ConfigureWebHost",
                wellKnownTypes.GenericHostWebHostBuilderExtensions))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DoNotUseConfigureWebHostWithConfigureHostBuilder,
                invocation.Syntax.GetLocation()));
        }
    }
}
