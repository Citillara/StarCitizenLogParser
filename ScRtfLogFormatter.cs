using StarCitizenLogParser;
using System.Text;

internal static class ScRtfLogFormatter
{
    private const string RtfHeader =
        @"{\rtf1\ansi{\colortbl ;\red0\green0\blue0;\red128\green128\blue128;}\fs20 "; // leave the trailing space !
    private const string BoldOn = @"\b ";
    private const string BoldOff = @"\b0 ";
    private const string GreyOn = @"\cf2 ";
    private const string ColorOff = @"\cf0 ";

    public static string ParseLogToRTF(string rawLog)
    {
        var sb = new StringBuilder(rawLog.Length * 2);
        sb.Append(RtfHeader);

        using var reader = new StringReader(rawLog);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var entry = ScReader.TryParse(line);
            if (entry is null) continue;

            // Localised, unambiguous timestamp
            sb.Append(EscapeRtf(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
            sb.Append(" - ");

            switch (entry)
            {
                case ActorDeathEntry a:
                    AppendActorDeath(sb, a);
                    break;

                case VehicleDestructionEntry v:
                    AppendVehicleDestruction(sb, v);
                    break;

                case HostilityEventEntry h:
                    AppendHostility(sb, h);
                    break;

                default:
                    sb.Append($"Unknown event: {EscapeRtf(entry.Kind.ToString())}");
                    break;
            }

            sb.Append(ColorOff).Append(@"\par ");
        }

        sb.Append('}');
        return sb.ToString();
    }

    /* ---------- helper blocks ---------- */

    private static void AppendActorDeath(StringBuilder sb, ActorDeathEntry a)
    {
        if (a.VictimName == a.KillerName && a.KillerName == a.WeaponName)
        {
            sb.Append(BoldOn).Append(EscapeRtf(ScNames.ToFriendly(a.KillerName)))
              .Append(BoldOff).Append(" backspaced");
            return;
        }

        if (a.VictimName.Contains("NPC", StringComparison.OrdinalIgnoreCase))
            sb.Append(GreyOn);

        sb.Append(BoldOn).Append(EscapeRtf(ScNames.ToFriendly(a.KillerName))).Append(BoldOff)
          .Append(" killed ")
          .Append(EscapeRtf(ScNames.ToFriendly(a.VictimName)))
          .Append(" with ")
          .Append(EscapeRtf(ScNames.ToFriendly(a.WeaponName)))
          .Append(" in ")
          .Append(EscapeRtf(ScNames.ToFriendly(a.Zone)));
    }

    private static void AppendVehicleDestruction(StringBuilder sb, VehicleDestructionEntry v)
    {
        if (v.DriverName == v.CausedByName && v.DriverName == v.VehicleName)
        {
            sb.Append(BoldOn).Append(EscapeRtf(ScNames.ToFriendly(v.DriverName)))
              .Append(BoldOff).Append(" backspaced");
            return;
        }

        sb.Append(BoldOn).Append(EscapeRtf(ScNames.ToFriendly(v.CausedByName))).Append(BoldOff)
          .Append(" destroyed ")
          .Append(EscapeRtf(ScNames.ToFriendly(v.VehicleName)))
          .Append(" of ")
          .Append(EscapeRtf(ScNames.ToFriendly(v.DriverName)));

        if (!string.IsNullOrEmpty(v.Zone))
            sb.Append(" in ").Append(EscapeRtf(ScNames.ToFriendly(v.Zone)));
    }

    private static void AppendHostility(StringBuilder sb, HostilityEventEntry h)
    {
        if (h.SourceName.Contains("NPC", StringComparison.OrdinalIgnoreCase) ||
            h.TargetName.Contains("NPC", StringComparison.OrdinalIgnoreCase))
            sb.Append(GreyOn);

        sb.Append(BoldOn).Append(EscapeRtf(ScNames.ToFriendly(h.SourceName))).Append(BoldOff)
          .Append(" hit ")
          .Append(EscapeRtf(ScNames.ToFriendly(h.TargetName)))
          .Append(" (child element : ")
          .Append(EscapeRtf(ScNames.ToFriendly(h.ChildName)))
          .Append(" )");
    }

    private static string EscapeRtf(string s) =>
        s.Replace(@"\", @"\\").Replace("{", @"\{").Replace("}", @"\}");
}
