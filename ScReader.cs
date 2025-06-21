using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarCitizenLogParser
{
    public readonly record struct Vec3(double X, double Y, double Z);

    public enum EventKind { VehicleDestruction, ActorDeath }

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
        string CauseTagCategory)
        : LogEntry(Timestamp, EventKind.VehicleDestruction);

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
        string TeamTag2)
        : LogEntry(Timestamp, EventKind.ActorDeath);

    public static class ScReader
    {
        public static LogEntry? TryParse(string line)
        {
            LogEntry entry = null;
            if (string.IsNullOrWhiteSpace(line) || line[0] != '<')
                return null;

            // --- 1. timestamp ----------------------------------------------------
            var i = 1;
            var endTs = line.IndexOf('>');
            var timestamp = ParseTimestamp(line.AsSpan(i, endTs - i));

            // --- 2. find the event tag ------------------------------------------
            var eventTagStart = line.IndexOf('<', endTs);
            var eventTagEnd = line.IndexOf('>', eventTagStart + 1);

            if(eventTagEnd == -1)
                return null;
            var eventTag = line.Substring(eventTagStart + 1,
                                               eventTagEnd - eventTagStart - 1);

            var rest = line[(eventTagEnd + 1)..].TrimStart();

            if (eventTag == "Vehicle Destruction")
            {
                entry = ParseVehicleDestruction(rest, timestamp);
            }
            if (eventTag == "Actor Death")
            {
                entry = ParseActorDeath(rest, timestamp);
            }
            return entry;
        }

        // ---------------------------------------------------------------------
        //  helpers
        // ---------------------------------------------------------------------

        private static DateTimeOffset ParseTimestamp(ReadOnlySpan<char> slice)
            => DateTimeOffset.ParseExact(slice, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                                          CultureInfo.InvariantCulture);

        private static string TakeBetween(string src, string left, string right,
                                          ref int idx)
        {
            int istart = src.IndexOf(left, idx, StringComparison.InvariantCulture);
            if (istart == -1)
                throw new DataMisalignedException("Report this bug (start) and this line : " + src);
            int start = istart + left.Length;
            int end = src.IndexOf(right, start, StringComparison.InvariantCulture);
            if (end == -1)
                throw new DataMisalignedException("Report this bug (end) and this line : " + src);
            idx = end + right.Length;
            return src[start..end];
        }

        private static Tuple<Vec3, Vec3> ParseVec3(string src)
        {
            // src e.g. "x: -141105.640836, y: 312066.617001, z: 228710.641646"
            var parts = src.Split(new string[] { ",", "vel" }, StringSplitOptions.TrimEntries);
            double x = double.Parse(parts[0].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));
            double y = double.Parse(parts[1].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));
            double z = double.Parse(parts[2].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));

            if (src.Contains("vel"))
            {
                double x2 = double.Parse(parts[3].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));
                double y2 = double.Parse(parts[4].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));
                double z2 = double.Parse(parts[5].Split(':')[1], CultureInfo.GetCultureInfo("en-US"));
                return new Tuple<Vec3, Vec3>(new Vec3(x, y, z),  new Vec3(x2, y2, z2));
            }

            return new Tuple<Vec3, Vec3>(new Vec3(x, y, z), new Vec3(0, 0, 0));
        }

        // ---------------------------------------------------------------------
        //  vehicle destruction
        // ---------------------------------------------------------------------
        private static VehicleDestructionEntry? ParseVehicleDestruction(string src,
                                                                        DateTimeOffset ts)
        {
            var idx = 0;
            var vehicleName = TakeBetween(src, "Vehicle '", "'", ref idx);
            var vehicleId = TakeBetween(src, "[", "]", ref idx);
            var zone = TakeBetween(src, "in zone '", "'", ref idx);

            var posString = TakeBetween(src, "[pos ", "]", ref idx);
            var posVel = ParseVec3(posString);
            var position = posVel.Item1;
            var velocity = posVel.Item2;

            //var velString = TakeBetween(src, "vel ", "]", ref idx);
            //var velocity = ParseVec3(velString);

            var driverName = TakeBetween(src, "driven by '", "'", ref idx);
            var driverId = TakeBetween(src, "[", "]", ref idx);

            var fromLevel = int.Parse(TakeBetween(src,
                                      "advanced from destroy level ", " to ", ref idx));
            idx -=5;
            var toLevel = int.Parse(TakeBetween(src, " to ", " c", ref idx));

            var causedBy = TakeBetween(src, "aused by '", "'", ref idx);
            var causedId = TakeBetween(src, "[", "]", ref idx);

            var causeTag = TakeBetween(src, "with '", "'", ref idx);
            // the two trailing [tag][tag] groups
            var tagStart = src.IndexOf('[', idx);
            var tagTeam = TakeBetween(src, "[", "]", ref tagStart);
            var tagCat = TakeBetween(src, "[", "]", ref tagStart);

            return new VehicleDestructionEntry(ts, vehicleName, vehicleId, zone,
                                               position, velocity,
                                               driverName, driverId,
                                               fromLevel, toLevel,
                                               causedBy, causedId,
                                               tagTeam, tagCat);
        }

        // ---------------------------------------------------------------------
        //  actor death
        // ---------------------------------------------------------------------
        private static ActorDeathEntry? ParseActorDeath(string src,
                                                        DateTimeOffset ts)
        {

            /*
             * <2025-06-20T23:50:29.843Z> [Notice] <Actor Death> CActor::Kill: 'Rydianna' [326852421041] 
             * in zone 'MISC_Starlancer_TAC_4528531523558' killed by 'WhatIdo' [202153873895] 
             * using 'HRST_LaserBeam_Bespoke_4510244335981' [Class unknown]
             * with damage type 'VehicleDestruction' 
             * from direction x: 0.000000, y: 0.000000, z: 0.000000
             * [Team_ActorTech][Actor]
             * <2025-06-21T01:22:47.877Z> [Notice] <Actor Death> CActor::Kill: 
             * 'PU_Human_Enemy_GroundCombat_NPC_ASD_soldier_4529397865889' [4529397865889] 
             * in zone 'pyro1' killed by 'Rydianna' [326852421041] using 
             * 'behr_lmg_ballistic_01_4530103770551' [Class behr_lmg_ballistic_01] 
             * with damage type 'Bullet' from direction x: -0.592138, y: 0.206040, z: -0.779051 
             * [Team_ActorTech][Actor]
            */


            var idx = 0;
            var victim = TakeBetween(src, "Kill: '", "'", ref idx);
            var victimId = TakeBetween(src, "[", "]", ref idx);
            var zone = TakeBetween(src, "in zone '", "'", ref idx);

            var killer = TakeBetween(src, "killed by '", "'", ref idx);
            var killerId = TakeBetween(src, "[", "]", ref idx);

            var weapon = TakeBetween(src, "using '", "'", ref idx);
            // skip optional "[Class ...]" block if present
            if (src.AsSpan(idx).TrimStart().StartsWith("[Class"))
                idx = src.IndexOf(']', idx) + 1;

            var dmgType = TakeBetween(src, "with damage type '", "'", ref idx);

            var dirString = TakeBetween(src, "direction ", "[", ref idx);
            var direction = ParseVec3(dirString).Item1;
            idx -= 1;
            var tag1 = TakeBetween(src, "[", "]", ref idx);
            var tag2 = TakeBetween(src, "[", "]", ref idx);

            return new ActorDeathEntry(ts, victim, victimId, zone,
                                       killer, killerId, weapon, dmgType,
                                       direction, tag1, tag2);
        }
    }
}
