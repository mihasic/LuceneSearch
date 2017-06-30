﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.Owin
{
    /// <summary>
    /// Provides correct escaping for Path and PathBase values when needed to reconstruct a request or redirect URI string
    /// </summary>
    public struct PathString : IEquatable<PathString>
    {
        private static Func<string, string> EscapeDataString = Uri.EscapeDataString;

        /// <summary>
        /// Represents the empty path. This field is read-only.
        /// </summary>
        public static readonly PathString Empty = new PathString(String.Empty);

        private readonly string _value;

        /// <summary>
        /// Initialize the path string with a given value. This value must be in un-escaped format. Use
        /// PathString.FromUriComponent(value) if you have a path value which is in an escaped format.
        /// </summary>
        /// <param name="value">The unescaped path to be assigned to the Value property.</param>
        public PathString(string value)
        {
            if (!String.IsNullOrEmpty(value) && value[0] != '/')
            {
                throw new ArgumentException(Resources.Exception_PathMustStartWithSlash, "value");
            }
            _value = value;
        }

        /// <summary>
        /// The unescaped path value
        /// </summary>
        public string Value
        {
            get { return _value; }
        }

        /// <summary>
        /// True if the path is not empty
        /// </summary>
        public bool HasValue
        {
            get { return !String.IsNullOrEmpty(_value); }
        }

        /// <summary>
        /// Provides the path string escaped in a way which is correct for combining into the URI representation. 
        /// </summary>
        /// <returns>The escaped path value</returns>
        public override string ToString()
        {
            return ToUriComponent();
        }

        /// <summary>
        /// Provides the path string escaped in a way which is correct for combining into the URI representation.
        /// </summary>
        /// <returns>The escaped path value</returns>
        [SuppressMessage("Microsoft.Design", "CA1055:UriReturnValuesShouldNotBeStrings", Justification = "Purpose of the method is to return a string")]
        public string ToUriComponent()
        {
            if (HasValue)
            {
                if (RequiresEscaping(_value))
                {
                    // TODO: Measure the cost of this escaping and consider optimizing.
                    return String.Join("/", _value.Split('/').Select(EscapeDataString));
                }
                return _value;
            }
            return String.Empty;
        }

        // Very conservative, these characters do not need to be escaped in a path.
        private static bool RequiresEscaping(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                // Check conservatively for safe characters. See http://www.ietf.org/rfc/rfc3986.txt
                bool safeChar =
                    (('a' <= c && c <= 'z')
                    || ('A' <= c && c <= 'Z')
                    || ('0' <= c && c <= '9')
                    || c == '/' || c == '-' || c == '_');
                if (!safeChar)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns an PathString given the path as it is escaped in the URI format. The string MUST NOT contain any
        /// value that is not a path.
        /// </summary>
        /// <param name="uriComponent">The escaped path as it appears in the URI format.</param>
        /// <returns>The resulting PathString</returns>
        [SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "Requirements not compatible with Uri processing")]
        public static PathString FromUriComponent(string uriComponent)
        {
            // REVIEW: what is the exactly correct thing to do?
            return new PathString(Uri.UnescapeDataString(uriComponent));
        }

        /// <summary>
        /// Returns an PathString given the path as from a Uri object. Relative Uri objects are not supported.
        /// </summary>
        /// <param name="uri">The Uri object</param>
        /// <returns>The resulting PathString</returns>
        public static PathString FromUriComponent(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }
            // REVIEW: what is the exactly correct thing to do?
            return new PathString("/" + uri.GetComponents(UriComponents.Path, UriFormat.Unescaped));
        }

        /// <summary>
        /// Checks if this instance starts with or exactly matches the other instance. Only full segments are matched.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool StartsWithSegments(PathString other)
        {
            string value1 = Value ?? String.Empty;
            string value2 = other.Value ?? String.Empty;
            if (value1.StartsWith(value2, StringComparison.OrdinalIgnoreCase))
            {
                return value1.Length == value2.Length || value1[value2.Length] == '/';
            }
            return false;
        }

        /// <summary>
        /// Checks if this instance starts with or exactly matches the other instance. Only full segments are matched.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="remaining">Any remaining segments from this instance not included in the other instance.</param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#", Justification = "Secondary information needed after boolean result obtained")]
        public bool StartsWithSegments(PathString other, out PathString remaining)
        {
            string value1 = Value ?? String.Empty;
            string value2 = other.Value ?? String.Empty;
            if (value1.StartsWith(value2, StringComparison.OrdinalIgnoreCase))
            {
                if (value1.Length == value2.Length || value1[value2.Length] == '/')
                {
                    remaining = new PathString(value1.Substring(value2.Length));
                    return true;
                }
            }
            remaining = Empty;
            return false;
        }

        /// <summary>
        /// Adds two PathString instances into a combined PathString value. 
        /// </summary>
        /// <returns>The combined PathString value</returns>
        public PathString Add(PathString other)
        {
            return new PathString(Value + other.Value);
        }

        /// <summary>
        /// Combines a PathString and QueryString into the joined URI formatted string value. 
        /// </summary>
        /// <returns>The joined URI formatted string value</returns>
        public string Add(QueryString other)
        {
            return ToUriComponent() + other.ToUriComponent();
        }

        /// <summary>
        /// Compares this PathString value to another value. The default comparison is StringComparison.OrdinalIgnoreCase.
        /// </summary>
        /// <param name="other">The second PathString for comparison.</param>
        /// <returns>True if both PathString values are equal</returns>
        public bool Equals(PathString other)
        {
            return string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compares this PathString value to another value using a specific StringComparison type
        /// </summary>
        /// <param name="other">The second PathString for comparison</param>
        /// <param name="comparisonType">The StringComparison type to use</param>
        /// <returns>True if both PathString values are equal</returns>
        public bool Equals(PathString other, StringComparison comparisonType)
        {
            return string.Equals(_value, other._value, comparisonType);
        }

        /// <summary>
        /// Compares this PathString value to another value. The default comparison is StringComparison.OrdinalIgnoreCase.
        /// </summary>
        /// <param name="obj">The second PathString for comparison.</param>
        /// <returns>True if both PathString values are equal</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is PathString && Equals((PathString)obj, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the hash code for the PathString value. The hash code is provided by the OrdinalIgnoreCase implementation.
        /// </summary>
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            return (_value != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(_value) : 0);
        }

        /// <summary>
        /// Operator call through to Equals
        /// </summary>
        /// <param name="left">The left parameter</param>
        /// <param name="right">The right parameter</param>
        /// <returns>True if both PathString values are equal</returns>
        public static bool operator ==(PathString left, PathString right)
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Operator call through to Equals
        /// </summary>
        /// <param name="left">The left parameter</param>
        /// <param name="right">The right parameter</param>
        /// <returns>True if both PathString values are not equal</returns>
        public static bool operator !=(PathString left, PathString right)
        {
            return !left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Operator call through to Add
        /// </summary>
        /// <param name="left">The left parameter</param>
        /// <param name="right">The right parameter</param>
        /// <returns>The PathString combination of both values</returns>
        public static PathString operator +(PathString left, PathString right)
        {
            return left.Add(right);
        }

        /// <summary>
        /// Operator call through to Add
        /// </summary>
        /// <param name="left">The left parameter</param>
        /// <param name="right">The right parameter</param>
        /// <returns>The PathString combination of both values</returns>
        public static string operator +(PathString left, QueryString right)
        {
            return left.Add(right);
        }
    }
}
