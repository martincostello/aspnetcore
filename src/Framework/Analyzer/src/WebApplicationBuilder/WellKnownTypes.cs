// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Analyzers.WebApplicationBuilder;

internal sealed class WellKnownTypes
{
    public static bool TryCreate(Compilation compilation, [NotNullWhen(true)] out WellKnownTypes? wellKnownTypes)
    {
        wellKnownTypes = default;

        const string WebApplication = "Microsoft.AspNetCore.Builder.WebApplication";
        if (compilation.GetTypeByMetadataName(WebApplication) is not { } webApplication)
        {
            return false;
        }

        const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
        if (compilation.GetTypeByMetadataName(WebApplicationBuilder) is not { } webApplicationBuilder)
        {
            return false;
        }

        const string ConfigureHostBuilder = "Microsoft.AspNetCore.Builder.ConfigureHostBuilder";
        if (compilation.GetTypeByMetadataName(ConfigureHostBuilder) is not { } configureHostBuilder)
        {
            return false;
        }

        const string ConfigureWebHostBuilder = "Microsoft.AspNetCore.Builder.ConfigureWebHostBuilder";
        if (compilation.GetTypeByMetadataName(ConfigureWebHostBuilder) is not { } configureWebHostBuilder)
        {
            return false;
        }

        wellKnownTypes = new WellKnownTypes
        {
            WebApplication = webApplication,
            WebApplicationBuilder = webApplicationBuilder,
            ConfigureHostBuilder = configureHostBuilder,
            ConfigureWebHostBuilder = configureWebHostBuilder,
        };

        return true;
    }

    public INamedTypeSymbol WebApplication { get; private init; }
    public INamedTypeSymbol WebApplicationBuilder { get; private init; }
    public INamedTypeSymbol ConfigureHostBuilder { get; private init; }
    public INamedTypeSymbol ConfigureWebHostBuilder { get; private init; }
}
