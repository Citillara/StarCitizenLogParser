using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StarCitizenLogParser
{
    public static class ScNames
    {
        // Loaded once at start-up; case-insensitive keys
        private static IDictionary<string, string> _friendly = LoadFriendlyTable();

        /// <summary>Re-reads the mapping file at runtime (e.g. after you edited it).</summary>
        /// <param name="path">Optional absolute or relative path to the JSON file.</param>
        public static void Reload(string? path = null) =>
            _friendly = LoadFriendlyTable(path);

        /// <summary>
        /// Cleans a raw Star Citizen asset code and returns a user-friendly name.
        /// </summary>
        /// <param name="raw">e.g. "HRST_LaserBeam_Bespoke_4510244335981"</param>
        /// <param name="custom">Optional extra or override lookup table</param>
        public static string ToFriendly(string raw,
                                        IDictionary<string, string>? custom = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw ?? string.Empty;

            // 1. Remove trailing "_digits"
            var cleaned = Regex.Replace(raw, @"_\d+$", string.Empty);

            // 2. Custom table first, then file-backed table
            if (custom != null && custom.TryGetValue(cleaned, out var friendly))
                return friendly;

            if (_friendly.TryGetValue(cleaned, out friendly))
                return friendly;

            // 3. Fallback: return the cleaned code unchanged (underscores kept)
            return cleaned;
        }

        // ------------------------- helpers -------------------------

        private static IDictionary<string, string> LoadFriendlyTable(string? path = null)
        {
            // Default: "./friendly_names.json" next to the executable
            path ??= Path.Combine(AppContext.BaseDirectory, "friendly_names.json");

            if (!File.Exists(path))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                return data != null
                    ? new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // If you prefer, log the exception somewhere…
                Console.Error.WriteLine($"[ScNames] Failed to load {path}: {ex.Message}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
