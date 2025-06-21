using Microsoft.Win32;
using System.Text;

namespace StarCitizenLogParser
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public static string ReadAllTextUnlocked(string path, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;

            using var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                // Allow other handles with read-or-write access, plus delete-pending files
                FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Please set the Star Citizen Log Path first");
                return;
            }
            var readtext = ReadAllTextUnlocked(path);

            string[] spl = readtext.Split(Environment.NewLine);

            StringBuilder sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi");

            // colour table: index 0 = “auto”, index 1 = black (default), index 2 = grey
            sb.Append(@"{\colortbl ;\red0\green0\blue0;\red128\green128\blue128;}");

            foreach (string line in spl)
            {
                LogEntry logEntry = ScReader.TryParse(line);

                if (logEntry != null)
                {
                    sb.Append(logEntry.Timestamp.ToString() + " - ");
                    switch (logEntry.Kind)
                    {
                        case EventKind.ActorDeath:
                            {
                                var a = (ActorDeathEntry)logEntry;

                                if (a.VictimName == a.KillerName && a.VictimName == a.WeaponName)
                                {
                                    sb.Append(@"\b ");
                                    sb.Append(ScNames.ToFriendly(a.KillerName));
                                    sb.Append(@"\b0 ");
                                    sb.Append(" backspaced");
                                    sb.Append(@"\par");
                                    sb.AppendLine();
                                    break;
                                }
                                if (a.VictimName.Contains("NPC"))
                                    sb.Append(@"\cf2 ");
                                sb.Append(@"\b ");                                   // start bold
                                sb.Append(ScNames.ToFriendly(a.KillerName));
                                sb.Append(@"\b0 ");                                  // end bold
                                sb.Append(" killed ");
                                sb.Append(ScNames.ToFriendly(a.VictimName));
                                sb.Append(" with ");
                                sb.Append(ScNames.ToFriendly(a.WeaponName));
                                sb.Append(" in ");
                                sb.Append(ScNames.ToFriendly(a.Zone));
                                sb.Append(@"\cf0 ");
                                sb.Append(@"\par");
                                sb.AppendLine();
                                break;
                            }
                        case EventKind.VehicleDestruction:
                            {
                                var v = (VehicleDestructionEntry)logEntry;

                                // 1) “Back-spaced” (driver, causer and vehicle have the same name)
                                if (v.DriverName == v.CausedByName && v.DriverName == v.VehicleName)
                                {
                                    sb.Append(@"\b ");
                                    sb.Append(ScNames.ToFriendly(v.CausedByName));
                                    sb.Append(@"\b0 ");
                                    sb.Append(" backspaced");
                                    sb.Append(@"\par");
                                    sb.AppendLine();
                                    break;
                                }

                                // 3) Normal destruction line
                                sb.Append(@"\b ");                                   // start bold
                                sb.Append(ScNames.ToFriendly(v.CausedByName));
                                sb.Append(@"\b0 ");                                  // end bold
                                sb.Append(" destroyed ");
                                sb.Append(ScNames.ToFriendly(v.VehicleName));
                                sb.Append(" of ");
                                sb.Append(ScNames.ToFriendly(v.DriverName));

                                // Optional: include the zone if your VehicleDestructionEntry exposes it
                                if (!string.IsNullOrEmpty(v.Zone))
                                {
                                    sb.Append(" in ");
                                    sb.Append(ScNames.ToFriendly(v.Zone));
                                }

                                sb.Append(@"\cf0 ");
                                sb.Append(@"\par");
                                sb.AppendLine();
                                break;
                            }
                        default: break;

                    }
                }

            }
            sb.Append("}");
            richTextBox1.Rtf = sb.ToString();
        }

        static readonly string RegistryRoot = @"Software\StarCitizenLogParser";
        //static readonly string DefaultPath = @"E:\Program Files\Robertspace Industries\StarCitizen\LIVE\Game.log";

        string path = string.Empty;

            
        private void Form1_Load(object sender, EventArgs e)
        {
            string reg = RegistryHelper.ReadRegistryValue(RegistryHive.CurrentUser, RegistryRoot, "SCFolderLocation", string.Empty);
            if (!string.IsNullOrEmpty(reg))
            {
                path = reg;
            }
            else
            {
                string? autolocate = ScLocator.FindInstallDir();
                if (autolocate != null && !string.IsNullOrEmpty(autolocate))
                {
                    path = autolocate;
                }
            }
            
            label1.Text = path;

        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Fallback to the user’s Documents folder if no directory is supplied
            string initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Ensure the fallback exists, otherwise OpenFileDialog complains
            if (!Directory.Exists(initialDirectory))
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            using var dlg = new OpenFileDialog
            {
                Title = "Locate Star Citizen Game.log",
                Filter = "Star Citizen log (Game.log)|Game.log|All files (*.*)|*.*",
                InitialDirectory = initialDirectory,
                CheckFileExists = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

            if(dlg.ShowDialog(this) == DialogResult.OK)
            {
                path = dlg.FileName;
                label1.Text = path;
                RegistryHelper.WriteRegistryValue(RegistryHive.CurrentUser, RegistryRoot, "SCFolderLocation", path);
            }
        }
    }
}
