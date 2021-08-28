// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Analyzers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking")]
internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor DoNotUseModelBindingAttributesOnMinimalActionParameters = new(
        "ASP0003",
        "Do not use model binding attributes with Map actions",
        "{0} should not be specified for a {1} delegate parameter",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://aka.ms/aspnet/analyzers");

    internal static readonly DiagnosticDescriptor DoNotUseConfigureWebHostWithConfigureHostBuilder = new(
        "ASP0005",
        "Do not use ConfigureWebHost with WebApplicationBuilder.Host",
        "ConfigureWebHost cannot be used with WebApplicationBuilder.Host",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://aka.ms/aspnet/analyzers");

    internal static readonly DiagnosticDescriptor DoNotUseConfigureWithConfigureWebHostBuilder = new(
        "ASP0006",
        "Do not use Configure with WebApplicationBuilder.WebHost",
        "Configure cannot be used with WebApplicationBuilder.WebHost",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://aka.ms/aspnet/analyzers");

    internal static readonly DiagnosticDescriptor DoNotUseUseStartupWithConfigureWebHostBuilder = new(
        "ASP0007",
        "Do not use UseStartup with WebApplicationBuilder.WebHost",
        "UseStartup cannot be used with WebApplicationBuilder.WebHost",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://aka.ms/aspnet/analyzers");
}
