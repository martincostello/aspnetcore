// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Analyzers.WebApplicationBuilder;

public partial class WebApplicationBuilderAnalyzer : DiagnosticAnalyzer
{
    private static void DisallowConfigureWebHostBuilderUseStartup(
        in OperationAnalysisContext context,
        WellKnownTypes wellKnownTypes,
        IInvocationOperation invocation,
        IMethodSymbol methodSymbol)
    {
        if (IsDisallowedMethod(
                context,
                invocation,
                methodSymbol,
                wellKnownTypes.ConfigureWebHostBuilder,
                "UseStartup",
                wellKnownTypes.HostingAbstractionsWebHostBuilderExtensions,
                wellKnownTypes.WebHostBuilderExtensions))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DoNotUseUseStartupWithConfigureWebHostBuilder,
                invocation.Syntax.GetLocation()));
        }
    }
}
