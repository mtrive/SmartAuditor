// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Shared conversion from loosely typed values (CLR scalars, numeric strings, and
    /// <see cref="JValue"/> instances after a JSON round-trip) into typed scalars.
    /// </summary>
    internal static class ValueCoercion
    {
        /// <summary>
        /// Returns true when a value should promote an insight-table total from long to double.
        /// </summary>
        public static bool UsesFloatingPointTotal(object value)
        {
            value = Unwrap(value);
            if (value == null)
                return false;

            if (value is float or double or decimal)
                return true;

            if (value is string str
                && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed != Math.Truncate(parsed);

            return false;
        }

        /// <summary>
        /// Formats a scalar value as invariant text. Handles <see cref="JValue"/> wrappers.
        /// </summary>
        public static string CoerceString(object value)
        {
            value = Unwrap(value);
            if (value == null)
                return string.Empty;

            if (value is string s)
                return s;

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// Parses an integral value. Does not accept fractional floating-point storage types.
        /// </summary>
        public static bool TryToLong(object value, out long result)
        {
            value = Unwrap(value);
            if (value == null)
            {
                result = 0;
                return false;
            }

            switch (value)
            {
                case long l: result = l; return true;
                case int i: result = i; return true;
                case short s: result = s; return true;
                case byte b: result = b; return true;
                case uint ui: result = ui; return true;
                case ushort us: result = us; return true;
                case sbyte sb: result = sb; return true;
                case ulong ul: result = unchecked((long)ul); return true;
                case bool boolean: result = boolean ? 1 : 0; return true;
                case string str:
                    return long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            }

            result = 0;
            return false;
        }

        /// <summary>
        /// Parses a floating-point value, including integral storage types and numeric strings.
        /// </summary>
        public static bool TryToDouble(object value, out double result)
        {
            value = Unwrap(value);
            if (value == null)
            {
                result = 0.0;
                return false;
            }

            switch (value)
            {
                case double d: result = d; return true;
                case float f: result = f; return true;
                case long l: result = l; return true;
                case int i: result = i; return true;
                case short s: result = s; return true;
                case byte b: result = b; return true;
                case uint ui: result = ui; return true;
                case ushort us: result = us; return true;
                case sbyte sb: result = sb; return true;
                case ulong ul: result = ul; return true;
                case decimal m: result = (double)m; return true;
                case bool boolean: result = boolean ? 1.0 : 0.0; return true;
                case string str:
                    return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            }

            result = 0.0;
            return false;
        }

        /// <summary>
        /// Parses an integral value, truncating fractional floating-point storage when needed.
        /// </summary>
        public static bool TryCoerceInt64(object value, out long result)
        {
            if (TryToLong(value, out result))
                return true;

            if (TryToDouble(value, out var asDouble))
            {
                result = (long)asDouble;
                return true;
            }

            result = 0;
            return false;
        }

        static object Unwrap(object value)
        {
            if (value is JValue jv)
                return jv.Value;

            return value;
        }
    }
}
