using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Text.Json;

namespace StellarisPlaysetSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string dbPath = "";
        bool dbLocated = false;
        bool dbLoaded = false;
        bool launcherDetected = false;
        bool dbBackedUp = false;
        Playset importedPlayset = null;
        public List<Playset> playsets { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            playsets = new List<Playset>();

            if (docsPath == "")
            {
                btnShowDatabase.IsEnabled = false;
                tbStep1.Text = "Failed to load Documents folder";
                tbStep1.Foreground = new SolidColorBrush(Colors.Red);
            }
            if (!docsPath.EndsWith(Path.DirectorySeparatorChar) && !docsPath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                docsPath += Path.DirectorySeparatorChar;
            }
            dbPath = docsPath + "Paradox Interactive\\Stellaris\\launcher-v2.sqlite";

            if (!File.Exists(dbPath))
            {
                btnShowDatabase.IsEnabled = false;
                tbStep1.Text = "Failed to find launcher DB";
                tbStep1.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                tbStep1.Text = "Launcher DB located, click to load";
                tbStep1.Foreground = new SolidColorBrush(Colors.Blue);
                dbLocated = true;
            }
            if (dbLocated)
                checkLauncherProcess();
            // Make a backup of the database
            if (!launcherDetected)
            {
                File.Copy(dbPath, dbPath + "_sps_backup", true);
                dbBackedUp = true;
            }
        }

        void ImportPlayset(string file)
        {
            try
            {
                importedPlayset = null;
                btnSave.IsEnabled = false;
                tbStep3.Text = "Import a playset before continuing";
                tbPsName.Text = "";
                string contents = File.ReadAllText(file);
                importedPlayset = JsonSerializer.Deserialize<Playset>(contents);
                btnSave.IsEnabled = true;
                tbStep3.Text = "Ready to save playset";
                tbStep3.Foreground = new SolidColorBrush(Colors.Blue);
                tbPsName.Text = "Source: " + file;
            }
            catch (JsonException ex)
            {
                MessageBox.Show("That file didn't quite work. Make sure you selected a playset file. \n" + ex.Message,
                    "Error reading file");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong! \n" + ex.Message, "Error");
            }
        }

        void checkLauncherProcess()
        {
            if (Process.GetProcessesByName("Paradox Launcher").Length > 0)
            {
                launcherDetected = true;
                btnShowDatabase.IsEnabled = false;
                tbStep1.Text = "Close Stellaris launcher";
                tbStep1.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (dbLocated)
            {
                launcherDetected = false;
                btnShowDatabase.IsEnabled = true;
                if (!dbLoaded)
                {
                    tbStep1.Text = "Import launcher data";
                    tbStep1.Foreground = new SolidColorBrush(Colors.Blue);
                }
                else
                {
                    tbStep1.Text = "Loaded launcher data";
                    tbStep1.Foreground = new SolidColorBrush(Colors.Green);
                }
                if (!dbBackedUp)
                {
                    File.Copy(dbPath, dbPath + "_sps_backup", true);
                    dbBackedUp = true;
                }
            }
        }


        private void mainWindow_Activated(object sender, EventArgs e)
        {
            checkLauncherProcess();
        }

        private void mainWindow_Deactivated(object sender, EventArgs e)
        {
            checkLauncherProcess();
        }

        private void btnShowDatabase_Click(object sender, RoutedEventArgs e)
        {
            checkLauncherProcess();
            if (launcherDetected || !dbLocated)
                return;
            var conBuilder = new SqliteConnectionStringBuilder();
            conBuilder.DataSource = dbPath;
            using (var con = new SqliteConnection(conBuilder.ConnectionString))
            {
                con.Open();
                var psetSelect = con.CreateCommand();
                psetSelect.CommandText = "SELECT * from playsets";
                using (var reader = psetSelect.ExecuteReader())
                {
                    while (reader.Read())
                        playsets.Add(new Playset(reader.GetString(0), reader.GetString(1)));
                }
            }
            lbPlaysets.ItemsSource = playsets;
            dbLoaded = true;
            tbStep1.Text = "Loaded launcher data";
            tbStep1.Foreground = new SolidColorBrush(Colors.Green);
            btnImport.IsEnabled = true;
        }

        private void lbPlaysets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!dbLoaded)
                return;
            btnExport.IsEnabled = true;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            Playset playset = (Playset)lbPlaysets.SelectedItem;
            playset.Mods = new List<Mod>();
            var conBuilder = new SqliteConnectionStringBuilder();
            conBuilder.DataSource = dbPath;

            using (var con = new SqliteConnection(conBuilder.ConnectionString))
            {
                con.Open();
                var psetSelect = con.CreateCommand();
                List<PlaysetMod> psetMods = new List<PlaysetMod>();
                List<string> psetModIds = new List<string>();
                psetSelect.CommandText = "SELECT * FROM playsets_mods WHERE playsetId=@psid";
                psetSelect.Parameters.Add("@psid", SqliteType.Text).Value = playset.Id;
                using (var reader = psetSelect.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        PlaysetMod pm = new PlaysetMod();
                        pm.PlaysetId = reader.GetString(0);
                        if (pm.PlaysetId != playset.Id)
                        {
                            throw new Exception("Mod playset ID did not match Playset ID!");
                        }
                        pm.ModId = reader.GetString(1);
                        pm.Position = reader.GetString(2);
                        pm.Enabled = reader.GetBoolean(3);
                        psetMods.Add(pm);
                        psetModIds.Add(pm.ModId);
                    }
                }
                var modSelect = con.CreateCommand();
                modSelect.CommandText = "SELECT * FROM mods";

                using (var reader = modSelect.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string id = reader.GetString(0);
                        if (psetModIds.Contains(id))
                        {
                            // This mod is in the exporting playset
                            string steamid = (string)reader["steamId"];
                            string name = (string)reader["displayName"];
                            PlaysetMod pm = psetMods.Where(m => m.ModId == id).First();
                            Mod m = new Mod(id, steamid, name, pm.Position, pm.Enabled);
                            playset.Mods.Add(m);
                        }
                    }
                }
            }
            playset.Export();
        }

        private void btnImport_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1 || !files[0].EndsWith(".json"))
                {
                    return;
                }
                ImportPlayset(files[0]);
                btnImport.Content = "Import playset (or drag one here)";
            }
        }

        private void btnImport_DragLeave(object sender, DragEventArgs e)
        {
            btnImport.Content = "Import playset (or drag one here)";
        }

        private void btnImport_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                {
                    btnImport.Content = "Too many files selected";
                    return;
                }
                if (!files[0].EndsWith(".json"))
                {
                    btnImport.Content = "Only .json files accepted";
                    return;
                }
                btnImport.Content = "Drop file to import";
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Import playset";
            ofd.DefaultExt = ".json";
            ofd.Filter = "JSON (.json)|*.json";

            bool? result = ofd.ShowDialog();
            if (result == true)
            {
                string file = ofd.FileName;
                if (!File.Exists(file))
                    return;
                ImportPlayset(file);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (Process.GetProcessesByName("Paradox Launcher").Length > 0)
            {
                MessageBox.Show("Close the Paradox Launcher before saving.", "Error");
                return;
            }

            // 1. Add the playset to playsets table
            // 2. Add any mods not in mods table to mods table with to_install status and inform user they need to download the mods
            // 3. Add mods to playset_mods table
            var conBuilder = new SqliteConnectionStringBuilder();
            conBuilder.DataSource = dbPath;
            List<Mod> missingMods = new List<Mod>();
            using (var con = new SqliteConnection(conBuilder.ConnectionString))
            {
                con.Open();
                // Adding the playset will require multiple commands
                using (var transaction = con.BeginTransaction())
                {
                    // Resolve name conflicts (where IDs are different)
                    if (playsets.Where(p => p.Name == importedPlayset.Name && p.Id != importedPlayset.Id).Count() > 0)
                    {
                        importedPlayset.Name += "_Imported";
                    }
                    // Check if there are conflicting playset IDs
                    if (playsets.Where(p => p.Id == importedPlayset.Id).Count() > 0)
                    {
                        var psDel = con.CreateCommand();
                        psDel.CommandText = "DELETE FROM playsets WHERE id=@psid";
                        psDel.Parameters.Add("@psid", SqliteType.Text).Value = importedPlayset.Id;
                        psDel.ExecuteNonQuery();
                        psDel.CommandText = "DELETE FROM playsets_mods WHERE playsetId=@psid";
                        psDel.ExecuteNonQuery();
                    }
                    // 1. Add the playset
                    var pInsert = con.CreateCommand();
                    pInsert.CommandText = "INSERT INTO playsets VALUES(@id, @name, 0, 'custom', null, null, @createdon, null, null, null, 0, 0, null)";
                    pInsert.Parameters.Add("@id", SqliteType.Text).Value = importedPlayset.Id;
                    pInsert.Parameters.Add("@name", SqliteType.Text).Value = importedPlayset.Name;
                    pInsert.Parameters.Add("@createdon", SqliteType.Integer).Value = DateTime.Now.Ticks;
                    pInsert.ExecuteNonQuery();

                    // 2.1 Check if mods need to be added to the mods table
                    foreach (Mod m in importedPlayset.Mods)
                    {
                        var modCheck = con.CreateCommand();
                        // Internal Mod IDs are GUIDs and might not be the same between PCs
                        // Steam ID of the mod must be checked instead
                        modCheck.CommandText = "SELECT id FROM mods WHERE steamid=@steamid";
                        modCheck.Parameters.Add("@steamid", SqliteType.Text).Value = m.steamId;
                        using (var reader = modCheck.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string modId = reader.GetString(0);
                                // Respect the local mod ID if it exists
                                if (modId != m.Id)
                                {
                                    m.Id = modId;
                                }
                            }
                            else
                            {
                                // If the reader did not read a row, then this mod is not on this computer
                                // Track it to notify the user
                                missingMods.Add(m);
                            }
                        }

                        // We need to add the mod, if missing, before adding to playsets_mods due to foreign key restraint
                        if (missingMods.Contains(m))
                        {
                            var fixMissing = con.CreateCommand();
                            fixMissing.CommandText = "INSERT INTO mods (id, steamId, displayName, status, source) VALUES(@id, @steamid, @name, 'to_install', 'steam')";
                            fixMissing.Parameters.Add("@id", SqliteType.Text).Value = m.Id;
                            fixMissing.Parameters.Add("@steamid", SqliteType.Text).Value = m.steamId;
                            fixMissing.Parameters.Add("@name", SqliteType.Text).Value = m.DisplayName;
                            fixMissing.ExecuteNonQuery();
                        }

                        // Add mods to playsets_mods table
                        var modAdd = con.CreateCommand();
                        modAdd.CommandText = "INSERT INTO playsets_mods (playsetId, modId, position, enabled) VALUES(@psid, @mid, @position, @enabled)";
                        modAdd.Parameters.Add("@psid", SqliteType.Text).Value = importedPlayset.Id;
                        modAdd.Parameters.Add("@mid", SqliteType.Text).Value = m.Id;
                        modAdd.Parameters.Add("@position", SqliteType.Text).Value = m.PlaysetPosition;
                        modAdd.Parameters.Add("@enabled", SqliteType.Integer).Value = m.Enabled;

                        modAdd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                tbStep3.Foreground = new SolidColorBrush(Colors.Green);
                tbStep3.Text = "Playset saved";

                if (missingMods.Count > 0)
                {
                    MessageBox.Show("You might be missing some mods! A text editor will open with links to the mods you must subscribe to.", "Missing mod(s)");
                    string text = "Missing Mod List" + Environment.NewLine;
                    text += "Please make sure you subscribe to each BEFORE starting Stellaris. If you are already subscribed but you haven't opened the launcher since you have subscribed to the listed mods then you can ignore this message.";
                    foreach (Mod m in missingMods)
                    {
                        text += string.Format("{2}{0} - https://steamcommunity.com/sharedfiles/filedetails/?id={1}",
                            m.DisplayName, m.steamId, Environment.NewLine);
                    }
                    File.WriteAllText("missingmods.txt", text);
                    Process.Start("notepad.exe", Environment.CurrentDirectory + Path.DirectorySeparatorChar + "missingmods.txt");
                }
            }
        }
    }
}
