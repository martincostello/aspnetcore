// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        if (!IsConfigureWebHostInvocation(wellKnownTypes, methodSymbol))
        {
            return;
        }

        var receiverType = GetReceiverType(
            invocation,
            context.CancellationToken);

        if (!SymbolEqualityComparer.Default.Equals(receiverType, wellKnownTypes.ConfigureHostBuilder))
        {
            return;
        }

        var location = Location.None;

        if (invocation.Syntax is not null)
        {
            location = invocation.Syntax.GetLocation();
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DoNotUseConfigureWebHostWithConfigureHostBuilder,
            location));
    }

    private static bool IsConfigureWebHostInvocation(
        WellKnownTypes wellKnownTypes,
        IMethodSymbol targetMethod)
    {
        return targetMethod is not null &&
            targetMethod.Name.Equals("ConfigureWebHost", StringComparison.Ordinal) &&
            SymbolEqualityComparer.Default.Equals(wellKnownTypes.GenericHostWebHostBuilderExtensions, targetMethod.ContainingType);
    }
}
