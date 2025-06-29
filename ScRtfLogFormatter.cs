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

        HostilityEventEntry? previousHostilityEventEntry = null;
        int hostilityEventCounter = 0;

        while ((line = reader.ReadLine()) != null)
        {
            var entry = ScReader.TryParse(line);
            if (entry is null) continue;

            // Immediately reset if it's another type of event and append the counter immediately
            if (previousHostilityEventEntry != null && entry.Kind != EventKind.HostilityEvent)
            {
                AppendCounter(sb, hostilityEventCounter);
                sb.Append(ColorOff).Append(@"\par ");
                // Localised, unambiguous timestamp
                sb.Append(EscapeRtf(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                sb.Append(" - ");
                previousHostilityEventEntry = null;
                hostilityEventCounter = 0;
            }
            else if (previousHostilityEventEntry != null && entry.Kind == EventKind.HostilityEvent)
            {
                // skip timestamp
            } 
            else
            {
                // Localised, unambiguous timestamp
                sb.Append(EscapeRtf(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                sb.Append(" - ");
            }



            switch (entry)
            {
                case ActorDeathEntry a:
                    AppendActorDeath(sb, a);
                    sb.Append(ColorOff).Append(@"\par ");
                    break;

                case VehicleDestructionEntry v:
                    AppendVehicleDestruction(sb, v);
                    sb.Append(ColorOff).Append(@"\par ");
                    break;

                case HostilityEventEntry h:
                    if (previousHostilityEventEntry == null)
                    {
                        AppendHostility(sb, h);
                        previousHostilityEventEntry = h;
                    }
                    else
                    {
                        if(AreDisplayEquals(h, previousHostilityEventEntry))
                        {
                            hostilityEventCounter++;
                            continue; // Skip to next loop, don't append anything
                        }
                        else
                        {
                            if(hostilityEventCounter > 0)
                                AppendCounter(sb, hostilityEventCounter);
                            sb.Append(ColorOff).Append(@"\par ");
                            // Localised, unambiguous timestamp
                            sb.Append(EscapeRtf(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                            sb.Append(" - ");
                            AppendHostility(sb, h);
                            previousHostilityEventEntry = h;
                            hostilityEventCounter = 0;
                        }
                    }

                    break;

                default:
                    sb.Append($"Unknown event: {EscapeRtf(entry.Kind.ToString())}");
                    sb.Append(ColorOff).Append(@"\par ");
                    break;
            }

        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendCounter(StringBuilder sb, int counter)
    {
        sb.Append($"(+ {counter} identical events)");
    }

    private static bool AreDisplayEquals(HostilityEventEntry h1, HostilityEventEntry h2)
    {
        return ((h1.SourceName == h2.SourceName)
            && (h1.TargetName == h2.TargetName)
            && (h1.ChildName == h2.ChildName));
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
