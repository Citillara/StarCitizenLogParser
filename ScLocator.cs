using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace StarCitizenLogParser;


public static class ScLocator
{
    public static string? FindInstallDir()
    {
        // 1) ────────── Registry ──────────
        string? regPath = FromRegistry();
        if (!string.IsNullOrWhiteSpace(regPath))
            return Clean(regPath);

        // 2) ─────────  libraries.xml ─────
        string? libPath = FromLauncherLibrary();
        if (!string.IsNullOrWhiteSpace(libPath))
            return Clean(libPath);

        // 3) ─────────  Drive crawl ───────
        string? drivePath = FromDriveScan();
        return Clean(drivePath);
    }

    // ---------- 1) Registry probe (updated) ----------
    private static string? FromRegistry()
    {
        const string uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        string? Probe(RegistryView view)
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = hklm.OpenSubKey(uninstall);
            if (key == null) return null;

            foreach (string subName in key.GetSubKeyNames())
            {
                using var app = key.OpenSubKey(subName);
                if (app == null) continue;

                string? name = app.GetValue("DisplayName") as string;
                if (name == null) continue;

                // Accept both "Star Citizen" AND "RSI Launcher <version>"
                bool looksLikeSC = name.Contains("Star Citizen", StringComparison.OrdinalIgnoreCase);
                bool looksLikeRSI = name.Contains("RSI Launcher", StringComparison.OrdinalIgnoreCase);
                if (!(looksLikeSC || looksLikeRSI)) continue;

                // The launcher entry’s InstallLocation is the launcher folder,
                // e.g.  C:\Program Files\Roberts Space Industries\RSI Launcher
                // Jump one level up and look for the StarCitizen\LIVE sibling.
                string? loc = app.GetValue("InstallLocation") as string
                              ?? app.GetValue("DisplayIcon") as string; // fallback
                if (string.IsNullOrWhiteSpace(loc)) continue;

                string root = Path.GetDirectoryName(
                                loc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                  ? loc            // DisplayIcon is often "…\RSI Launcher.exe"
                                  : Path.Combine(loc!, "dummy")); // keep Path.GetDirectoryName happy

                if (root == null) continue;

                // Try “…\StarCitizen\LIVE”
                string probe = Path.Combine(root, @"..\StarCitizen\LIVE");
                probe = Path.GetFullPath(probe);
                if (Directory.Exists(probe))
                    return probe;

                // Some users put the game inside the launcher folder itself
                probe = Path.Combine(root, @"StarCitizen\LIVE");
                if (Directory.Exists(probe))
                    return probe;
            }
            return null;
        }

        return Probe(RegistryView.Registry64) ?? Probe(RegistryView.Registry32);
    }
    // -------------------------------------------------

    private static string? FromLauncherLibrary()
    {
        string file = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.CommonApplicationData),
                        "rsilauncher", "libraries.xml");
        if (!File.Exists(file)) return null;

        var doc = XDocument.Load(file);
        var first = doc.Descendants("library").FirstOrDefault();
        return first?.Attribute("path")?.Value;
    }

    private static string? FromDriveScan()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            string candidate = Path.Combine(drive.RootDirectory.FullName,
                                            @"Roberts Space Industries\StarCitizen\LIVE");
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string? Clean(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim('"'));
}
