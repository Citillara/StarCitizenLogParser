using System;
using System.Globalization;

namespace StarCitizenLogParser;

//--------------------------------------------------
// value objects & record types
//--------------------------------------------------
public readonly record struct Vec3(double X, double Y, double Z);

public enum EventKind
{
    VehicleDestruction,
    ActorDeath,
    HostilityEvent
}

public abstract record LogEntry(DateTimeOffset Timestamp, EventKind Kind);

public sealed record VehicleDestructionEntry(
    DateTimeOffset Timestamp,
    string VehicleName,
    string VehicleId,
    string Zone,
    Vec3 Position,
    Vec3 Velocity,
    string DriverName,
    string DriverId,
    int FromLevel,
    int ToLevel,
    string CausedByName,
    string CausedById,
    string CauseTagTeam,
    string CauseTagCategory) : LogEntry(Timestamp, EventKind.VehicleDestruction);

public sealed record ActorDeathEntry(
    DateTimeOffset Timestamp,
    string VictimName,
    string VictimId,
    string Zone,
    string KillerName,
    string KillerId,
    string WeaponName,
    string DamageType,
    Vec3 Direction,
    string TeamTag1,
    string TeamTag2) : LogEntry(Timestamp, EventKind.ActorDeath);

/// <summary>
/// Debug-level fake-hit hostility event (friendly-fire/scripted hit).
/// </summary>
public sealed record HostilityEventEntry(
    DateTimeOffset Timestamp,
    string SourceName,
    string TargetName,
    string ChildName,
    string TeamTag1,
    string TeamTag2) : LogEntry(Timestamp, EventKind.HostilityEvent);

public static class ScReader
{
    //--------------------------------------------------
    // entry point
    //--------------------------------------------------
    public static LogEntry? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '<')
            return null;

        // 1. timestamp
        var tsEnd = line.IndexOf('>');
        if (tsEnd == -1) return null;
        var timestamp = ParseTimestamp(line.AsSpan(1, tsEnd - 1));

        // 2. event tag (second <...>)
        var tagStart = line.IndexOf('<', tsEnd + 1);
        var tagEnd = line.IndexOf('>', tagStart + 1);
        if (tagStart == -1 || tagEnd == -1) return null;
        var eventTag = line.Substring(tagStart + 1, tagEnd - tagStart - 1);

        var rest = line[(tagEnd + 1)..].TrimStart();

        return eventTag switch
        {
            "Vehicle Destruction" => ParseVehicleDestruction(rest, timestamp),
            "Actor Death" => ParseActorDeath(rest, timestamp),
            "Debug Hostility Events" => ParseHostilityEvent(rest, timestamp),
            _ => null
        };
    }

    //--------------------------------------------------
    // helpers
    //--------------------------------------------------
    private static DateTimeOffset ParseTimestamp(ReadOnlySpan<char> slice)
        => DateTimeOffset.ParseExact(
               slice,
               "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal);

    /// <summary>
    /// Consumes the right delimiter: on return <paramref name="idx"/> points *after* <c>right</c>.
    /// </summary>
    private static string TakeBetween(string src, string left, string right, ref int idx)
    {
        var l = src.IndexOf(left, idx, StringComparison.InvariantCulture);
        if (l == -1) throw new DataMisalignedException($"Could not find '{left}' starting at {idx} in: {src}");
        l += left.Length;

        var r = src.IndexOf(right, l, StringComparison.InvariantCulture);
        if (r == -1) throw new DataMisalignedException($"Could not find '{right}' starting at {idx} in: {src}");

        idx = r + right.Length;          // consume the right delimiter
        return src[l..r];
    }

    /// <summary>
    /// *Does not* consume the right delimiter: on return <paramref name="nextIdx"/> points *at* <c>right</c>.
    /// Useful when you want to examine what follows the right delimiter.
    /// </summary>
    private static string PeekBetween(string src, string left, string right, int startIdx, out int nextIdx)
    {
        var l = src.IndexOf(left, startIdx, StringComparison.InvariantCulture);
        if (l == -1) throw new DataMisalignedException($"Could not find '{left}' starting at {startIdx} in: {src}");
        l += left.Length;

        var r = src.IndexOf(right, l, StringComparison.InvariantCulture);
        if (r == -1) throw new DataMisalignedException($"Could not find '{right}' starting at {l} in: {src}");

        nextIdx = r;                      // leave idx on the right delimiter
        return src[l..r];
    }

    private static (Vec3 pos, Vec3 vel) ParseVec3(string src)
    {
        // Format example:
        // "x: -139862.462432, y: 368748.106432, z: 120479.549545 vel x: 50.927578, y: 0.616747, z: -66.203087"
        var parts = src.Split(" vel ", 2, StringSplitOptions.TrimEntries);
        var posVec = ParseTriple(parts[0]);
        var velVec = parts.Length == 2 ? ParseTriple(parts[1]) : new Vec3();
        return (posVec, velVec);

        static Vec3 ParseTriple(string segment)
        {
            var comps = segment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (comps.Length < 3)
                throw new FormatException($"Vector3 segment malformed: {segment}");

            double ParseComp(string kv) => double.Parse(kv.Split(':')[1], CultureInfo.GetCultureInfo("en-US"));

            var x = ParseComp(comps[0]);
            var y = ParseComp(comps[1]);
            var z = ParseComp(comps[2]);
            return new Vec3(x, y, z);
        }
    }

    //--------------------------------------------------
    // vehicle destruction
    //--------------------------------------------------
    private static VehicleDestructionEntry ParseVehicleDestruction(string src, DateTimeOffset ts)
    {
        var idx = 0;
        var vehicleName = TakeBetween(src, "Vehicle '", "'", ref idx);
        var vehicleId = TakeBetween(src, "[", "]", ref idx);
        var zone = TakeBetween(src, "in zone '", "'", ref idx);

        var posText = TakeBetween(src, "[pos ", "]", ref idx);
        var (pos, vel) = ParseVec3(posText);

        var driverName = TakeBetween(src, "driven by '", "'", ref idx);
        var driverId = TakeBetween(src, "[", "]", ref idx);
        var fromLevel = int.Parse(TakeBetween(src, "advanced from destroy level ", " to ", ref idx), CultureInfo.InvariantCulture);

        // --- safer extraction for ToLevel ---
        var end = src.IndexOf(' ', idx);
        if (end == -1)
            throw new FormatException($"Missing space after to-level in: {src}");
        var toLevel = int.Parse(src.AsSpan(idx, end - idx), CultureInfo.InvariantCulture);
        idx = end + 1;
        // -------------------------------------

        var causedBy = TakeBetween(src, "caused by '", "'", ref idx);
        var causedId = TakeBetween(src, "[", "]", ref idx);

        _ = TakeBetween(src, "with '", "'", ref idx); // unused
        var teamTag = TakeBetween(src, "[", "]", ref idx);
        var catTag = TakeBetween(src, "[", "]", ref idx);

        return new VehicleDestructionEntry(ts, vehicleName, vehicleId, zone,
                                           pos, vel,
                                           driverName, driverId,
                                           fromLevel, toLevel,
                                           causedBy, causedId,
                                           teamTag, catTag);
    }

    //--------------------------------------------------
    // actor death
    //--------------------------------------------------
    private static ActorDeathEntry ParseActorDeath(string src, DateTimeOffset ts)
    {
        var idx = 0;
        var victim = TakeBetween(src, "Kill: '", "'", ref idx);
        var victimId = TakeBetween(src, "[", "]", ref idx);
        var zone = TakeBetween(src, "in zone '", "'", ref idx);

        var killer = TakeBetween(src, "killed by '", "'", ref idx);
        var killerId = TakeBetween(src, "[", "]", ref idx);

        var weapon = TakeBetween(src, "using '", "'", ref idx);
        if (src.AsSpan(idx).TrimStart().StartsWith("[Class", StringComparison.InvariantCulture))
            idx = src.IndexOf(']', idx) + 1;

        var dmgType = TakeBetween(src, "with damage type '", "'", ref idx);

        // ---- fixed part: don't consume the '[' ----
        var dirText = PeekBetween(src, "direction ", "[", idx, out idx);
        var direction = ParseVec3(dirText).pos;
        // idx now sits on the '[' that starts the first team tag
        var tag1 = TakeBetween(src, "[", "]", ref idx);
        var tag2 = TakeBetween(src, "[", "]", ref idx);
        // -------------------------------------------

        return new ActorDeathEntry(ts, victim, victimId, zone,
                                   killer, killerId, weapon, dmgType,
                                   direction, tag1, tag2);
    }

    //--------------------------------------------------
    // debug hostility event
    //--------------------------------------------------
    private static HostilityEventEntry ParseHostilityEvent(string src, DateTimeOffset ts)
    {
        const string fromKey = " FROM ";
        const string toKey = " TO ";
        const string childKey = " child ";

        var fromIdx = src.IndexOf(fromKey, StringComparison.InvariantCulture);
        var toIdx = src.IndexOf(toKey, fromIdx + fromKey.Length, StringComparison.InvariantCulture);
        if (fromIdx == -1 || toIdx == -1)
            throw new FormatException($"Hostility line malformed (no FROM/TO): {src}");

        var source = src[(fromIdx + fromKey.Length)..toIdx].Trim();

        var afterTo = toIdx + toKey.Length;
        var periodIdx = src.IndexOf('.', afterTo);
        var target = (periodIdx == -1 ? src[afterTo..] : src[afterTo..periodIdx]).Trim();

        // Child (optional)
        string child = string.Empty;
        if (periodIdx != -1)
        {
            var childIdx = src.IndexOf(childKey, periodIdx, StringComparison.InvariantCulture);
            if (childIdx != -1)
            {
                var childStart = childIdx + childKey.Length;
                var childEnd = src.IndexOfAny([' ', '[', '.'], childStart);
                if (childEnd == -1) childEnd = src.Length;
                child = src[childStart..childEnd].Trim();
            }
        }

        // Final two [tag] groups – tolerant even if one or none present
        string tag1 = string.Empty, tag2 = string.Empty;
        var rightMost = src.LastIndexOf("]", StringComparison.InvariantCulture);
        if (rightMost != -1)
        {
            var leftMost = src.LastIndexOf('[', rightMost);
            if (leftMost != -1)
            {
                tag2 = src[(leftMost + 1)..rightMost];
                var prevRight = src.LastIndexOf(']', leftMost - 1);
                if (prevRight != -1)
                {
                    var prevLeft = src.LastIndexOf('[', prevRight);
                    if (prevLeft != -1)
                        tag1 = src[(prevLeft + 1)..prevRight];
                }
            }
        }

        return new HostilityEventEntry(ts, source, target, child, tag1, tag2);
    }
}
