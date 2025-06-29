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


            richTextBox1.Rtf = ScRtfLogFormatter.ParseLogToRTF(readtext);
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
