﻿//HintName: ValidatableInfoResolver.g.cs
#nullable enable annotations
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable

namespace System.Runtime.CompilerServices
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    file sealed class InterceptsLocationAttribute : System.Attribute
    {
        public InterceptsLocationAttribute(int version, string data)
        {
        }
    }
}

namespace Microsoft.AspNetCore.Http.Validation.Generated
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    file sealed class GeneratedValidatablePropertyInfo : global::Microsoft.AspNetCore.Http.Validation.ValidatablePropertyInfo
    {
        public GeneratedValidatablePropertyInfo(
            global::System.Type containingType,
            global::System.Type propertyType,
            string name,
            string displayName) : base(containingType, propertyType, name, displayName)
        {
            ContainingType = containingType;
            Name = name;
        }

        internal global::System.Type ContainingType { get; }
        internal string Name { get; }

        protected override global::System.ComponentModel.DataAnnotations.ValidationAttribute[] GetValidationAttributes()
            => ValidationAttributeCache.GetValidationAttributes(ContainingType, Name);
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    file sealed class GeneratedValidatableTypeInfo : global::Microsoft.AspNetCore.Http.Validation.ValidatableTypeInfo
    {
        public GeneratedValidatableTypeInfo(
            global::System.Type type,
            ValidatablePropertyInfo[] members) : base(type, members) { }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    file class GeneratedValidatableInfoResolver : global::Microsoft.AspNetCore.Http.Validation.IValidatableInfoResolver
    {
        public bool TryGetValidatableTypeInfo(global::System.Type type, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::Microsoft.AspNetCore.Http.Validation.IValidatableInfo? validatableInfo)
        {
            validatableInfo = null;
            if (type == typeof(global::DerivedType))
            {
                validatableInfo = CreateDerivedType();
                return true;
            }
            if (type == typeof(global::BaseType))
            {
                validatableInfo = CreateBaseType();
                return true;
            }
            if (type == typeof(global::DerivedValidatableType))
            {
                validatableInfo = CreateDerivedValidatableType();
                return true;
            }
            if (type == typeof(global::BaseValidatableType))
            {
                validatableInfo = CreateBaseValidatableType();
                return true;
            }
            if (type == typeof(global::ContainerType))
            {
                validatableInfo = CreateContainerType();
                return true;
            }

            return false;
        }

        // No-ops, rely on runtime code for ParameterInfo-based resolution
        public bool TryGetValidatableParameterInfo(global::System.Reflection.ParameterInfo parameterInfo, [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out global::Microsoft.AspNetCore.Http.Validation.IValidatableInfo? validatableInfo)
        {
            validatableInfo = null;
            return false;
        }

        private ValidatableTypeInfo CreateDerivedType()
        {
            return new GeneratedValidatableTypeInfo(
                type: typeof(global::DerivedType),
                members: [
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::DerivedType),
                        propertyType: typeof(string),
                        name: "Value3",
                        displayName: "Value3"
                    ),
                ]
            );
        }
        private ValidatableTypeInfo CreateBaseType()
        {
            return new GeneratedValidatableTypeInfo(
                type: typeof(global::BaseType),
                members: [
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::BaseType),
                        propertyType: typeof(int),
                        name: "Value1",
                        displayName: "Value 1"
                    ),
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::BaseType),
                        propertyType: typeof(string),
                        name: "Value2",
                        displayName: "Value2"
                    ),
                ]
            );
        }
        private ValidatableTypeInfo CreateDerivedValidatableType()
        {
            return new GeneratedValidatableTypeInfo(
                type: typeof(global::DerivedValidatableType),
                members: [
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::DerivedValidatableType),
                        propertyType: typeof(string),
                        name: "Value3",
                        displayName: "Value3"
                    ),
                ]
            );
        }
        private ValidatableTypeInfo CreateBaseValidatableType()
        {
            return new GeneratedValidatableTypeInfo(
                type: typeof(global::BaseValidatableType),
                members: []
            );
        }
        private ValidatableTypeInfo CreateContainerType()
        {
            return new GeneratedValidatableTypeInfo(
                type: typeof(global::ContainerType),
                members: [
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::ContainerType),
                        propertyType: typeof(global::BaseType),
                        name: "BaseType",
                        displayName: "BaseType"
                    ),
                    new GeneratedValidatablePropertyInfo(
                        containingType: typeof(global::ContainerType),
                        propertyType: typeof(global::BaseValidatableType),
                        name: "BaseValidatableType",
                        displayName: "BaseValidatableType"
                    ),
                ]
            );
        }

    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    file static class GeneratedServiceCollectionExtensions
    {
        [InterceptsLocation]
        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValidation(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Action<ValidationOptions>? configureOptions = null)
        {
            // Use non-extension method to avoid infinite recursion.
            return global::Microsoft.Extensions.DependencyInjection.ValidationServiceCollectionExtensions.AddValidation(services, options =>
            {
                options.Resolvers.Insert(0, new GeneratedValidatableInfoResolver());
                if (configureOptions is not null)
                {
                    configureOptions(options);
                }
            });
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.Http.ValidationsGenerator, Version=42.42.42.42, Culture=neutral, PublicKeyToken=adb9793829ddae60", "42.42.42.42")]
    file static class ValidationAttributeCache
    {
        private sealed record CacheKey(global::System.Type ContainingType, string PropertyName);
        private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<CacheKey, global::System.ComponentModel.DataAnnotations.ValidationAttribute[]> _cache = new();

        public static global::System.ComponentModel.DataAnnotations.ValidationAttribute[] GetValidationAttributes(
            global::System.Type containingType,
            string propertyName)
        {
            var key = new CacheKey(containingType, propertyName);
            return _cache.GetOrAdd(key, static k =>
            {
                var property = k.ContainingType.GetProperty(k.PropertyName);
                if (property == null)
                {
                    return [];
                }

                return [.. global::System.Reflection.CustomAttributeExtensions.GetCustomAttributes<global::System.ComponentModel.DataAnnotations.ValidationAttribute>(property, inherit: true)];
            });
        }
    }
}