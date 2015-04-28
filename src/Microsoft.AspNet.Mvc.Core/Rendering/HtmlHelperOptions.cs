﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.Mvc.Rendering
{
    /// <summary>
    /// Provides programmatic configuration for the HTML helpers and <see cref="ViewContext"/>.
    /// </summary>
    public class HtmlHelperOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="Html5DateRenderingMode.Html5DateRenderingMode"/> value.
        /// </summary>
        /// <remarks>
        /// Set this property to <see cref="Html5DateRenderingMode.Rfc3339"/> to have templated helpers such as
        /// <see cref="IHtmlHelper.Editor"/> and <see cref="IHtmlHelper{TModel}.EditorFor"/> render date and time 
        /// values as RFC 3339 compliant strings. By default these helpers render dates and times using the current 
        /// culture.
        /// </remarks>
        public Html5DateRenderingMode Html5DateRenderingMode { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="string"/> that replaces periods in the ID attribute of an element.
        /// </summary>
        public string IdAttributeDotReplacement { get; set; } = "_";

        /// <summary>
        /// Gets or sets a value that indicates whether client-side validation is enabled.
        /// </summary>
        public bool ClientValidationEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the element name used to wrap a top-level message generated by 
        /// <see cref="IHtmlHelper.ValidationMessage"/> and other overloads.
        /// </summary>
        public string ValidationMessageElement { get; set; } = "span";

        /// <summary>
        /// Gets or sets the element name used to wrap a top-level message generated by 
        /// <see cref="IHtmlHelper.ValidationSummary"/> and other overloads.
        /// </summary>
        public string ValidationSummaryMessageElement { get; set; } = "span";
    }
}
