using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarCitizenLogParser;

public static class RegistryHelper
{
    /// <summary>
    /// Writes (or overwrites) a value under the specified key.
    /// </summary>
    /// <param name="hive">Root hive (e.g., RegistryHive.CurrentUser).</param>
    /// <param name="subKey">Path below the hive (e.g., @"Software\MyApp").</param>
    /// <param name="valueName">Name of the value to write.</param>
    /// <param name="value">Any primitive, string, or byte[] you want to store.</param>
    /// <param name="view">32- or 64-bit view; leave default unless you have a reason.</param>
    public static void WriteRegistryValue(
        RegistryHive hive,
        string subKey,
        string valueName,
        object value,
        RegistryView view = RegistryView.Default)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.CreateSubKey(subKey, writable: true);
        key.SetValue(valueName, value ?? string.Empty);
    }

    /// <summary>
    /// Reads a value and converts it to the requested type.
    /// Returns <paramref name="defaultValue"/> if the key or value is missing
    /// **or** if the stored data is null/empty or cannot be converted.
    /// </summary>
    public static T ReadRegistryValue<T>(
        RegistryHive hive,
        string subKey,
        string valueName,
        T defaultValue,
        RegistryView view = RegistryView.Default)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(subKey, writable: false);

        if (key == null) return defaultValue;

        object raw = key.GetValue(valueName, null);
        if (raw == null) return defaultValue;

        // Treat empty strings as “not set”
        if (raw is string s && string.IsNullOrWhiteSpace(s)) return defaultValue;

        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
