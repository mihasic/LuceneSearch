﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Owin
{
    /// <summary>
    /// Represents a wrapper for owin.RequestHeaders and owin.ResponseHeaders.
    /// </summary>
    public interface IHeaderDictionary : IReadableStringCollection, IDictionary<string, string[]>
    {
        /// <summary>
        /// Get or sets the associated value from the collection as a single string.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <returns>the associated value from the collection as a single string or null if the key is not present.</returns>
        new string this[string key] { get; set; }

        /// <summary>
        /// Get the associated values from the collection separated into individual values.
        /// Quoted values will not be split, and the quotes will be removed.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <returns>the associated values from the collection separated into individual values, or null if the key is not present.</returns>
        IList<string> GetCommaSeparatedValues(string key);

        /// <summary>
        /// Add a new value. Appends to the header if already present
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="value">The header value.</param>
        void Append(string key, string value);

        /// <summary>
        /// Add new values. Each item remains a separate array entry.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="values">The header values.</param>
        void AppendValues(string key, params string[] values);

        /// <summary>
        /// Quotes any values containing comas, and then coma joins all of the values with any existing values.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="values">The header values.</param>
        void AppendCommaSeparatedValues(string key, params string[] values);

        /// <summary>
        /// Sets a specific header value.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="value">The header value.</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set", Justification = "Re-evaluate later.")]
        void Set(string key, string value);

        /// <summary>
        /// Sets the specified header values without modification.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="values">The header values.</param>
        void SetValues(string key, params string[] values);

        /// <summary>
        /// Quotes any values containing comas, and then coma joins all of the values.
        /// </summary>
        /// <param name="key">The header name.</param>
        /// <param name="values">The header values.</param>
        void SetCommaSeparatedValues(string key, params string[] values);
    }
}
