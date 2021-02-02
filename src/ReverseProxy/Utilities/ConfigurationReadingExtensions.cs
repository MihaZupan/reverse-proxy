using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Microsoft.Extensions.Configuration
{
    internal static class ConfigurationReadingExtensions
    {
        internal static int? ReadInt32(this IConfiguration configuration, string name)
        {
            return configuration[name] is string value ? int.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : default(int?);
        }

        internal static double? ReadDouble(this IConfiguration configuration, string name)
        {
            return configuration[name] is string value ? double.Parse(value, CultureInfo.InvariantCulture) : default(double?);
        }

        internal static TimeSpan? ReadTimeSpan(this IConfiguration configuration, string name)
        {
            return configuration[name] is string value ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : default(TimeSpan?);
        }

        internal static TEnum? ReadEnum<TEnum>(this IConfiguration configuration, string name) where TEnum : struct
        {
            return configuration[name] is string value ? Enum.Parse<TEnum>(value, ignoreCase: true) : default(TEnum?);
        }

        internal static bool? ReadBool(this IConfiguration configuration, string name)
        {
            return configuration[name] is string value ? bool.Parse(value) : default(bool?);
        }

        internal static Version ReadVersion(this IConfiguration configuration, string name)
        {
            return configuration[name] is string value && !string.IsNullOrEmpty(value) ? Version.Parse(value + (value.Contains('.') ? "" : ".0")) : null;
        }

        internal static IReadOnlyDictionary<string, string> ReadStringDictionary(this IConfigurationSection section)
        {
            if (section.GetChildren() is var children && !children.Any())
            {
                return null;
            }

            return new ReadOnlyDictionary<string, string>(children.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase));
        }

        internal static string[] ReadStringArray(this IConfigurationSection section)
        {
            if (section.GetChildren() is var children && !children.Any())
            {
                return null;
            }

            return children.Select(s => s.Value).ToArray();
        }
    }
}
