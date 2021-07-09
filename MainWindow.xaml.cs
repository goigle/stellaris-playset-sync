using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace StellarisPlaysetSync
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private SteamWorkshopDownloader SWD;
		private PopupWindow popupWindow;
		string dbPath = "";
		bool dbLocated = false;
		bool dbLoaded = false;
		bool launcherDetected = false;
		bool dbBackedUp = false;
		Playset importedPlayset;
		Playset selectedPlayset;
		public List<Playset> playsets { get; set; }
		private List<ulong> missingModList { get; set; }

		public MainWindow()
		{
			InitializeComponent();
			popupWindow = new PopupWindow();
			missingModList = new List<ulong>();
			SWD = new SteamWorkshopDownloader();
			if (SWD.isConnected)
			{
				steam_stat.Text = "Steam:✔";
			}

			string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			playsets = new List<Playset>();

			if (docsPath == "")
			{
				button_pdx_import.IsEnabled = false;
				pdx_db_stat.Text = "Failed to load Documents folder";
			}
			if (!docsPath.EndsWith(Path.DirectorySeparatorChar) && !docsPath.EndsWith(Path.AltDirectorySeparatorChar))
			{
				docsPath += Path.DirectorySeparatorChar;
			}
			dbPath = docsPath + "Paradox Interactive\\Stellaris\\launcher-v2.sqlite";

			if (!File.Exists(dbPath))
			{
				button_pdx_import.IsEnabled = false;
				pdx_db_stat.Text = "Failed to find launcher DB";
			}
			else
			{
				pdx_db_stat.Text = "Launcher DB located";
				dbLocated = true;
			}
			if (dbLocated && LoadDB())
			{
				dataGrid_Playsets.ItemsSource = playsets;
				pdx_db_stat.Text = "Loaded launcher data";
				button_SPST_import.IsEnabled = true;
				button_SPST_import_clipboard.IsEnabled = true;
			}
			// Make a backup of the database
			if (!launcherDetected)
			{
				File.Copy(dbPath, dbPath + "_sps_backup", true);
				dbBackedUp = true;
			}
		}

		private void CheckLauncherProcess()
		{
			if (Process.GetProcessesByName("Paradox Launcher").Length > 0)
			{
				launcherDetected = true;
				button_pdx_import.IsEnabled = false;
				pdx_db_stat.Text = "Close Stellaris launcher!";
				return;
			}
			pdx_db_stat.Text = "...";
			if (dbLocated)
			{
				launcherDetected = false;
				if (!dbLoaded)
				{
					button_pdx_import.Content = "Import Stellaris Launcher Playset";
					button_pdx_import.IsEnabled = true;
				}
				else
				{
					pdx_db_stat.Text = "Loaded launcher data";
					button_pdx_import.IsEnabled = false;
				}
				if (!dbBackedUp)
				{
					File.Copy(dbPath, dbPath + "_sps_backup", true);
					dbBackedUp = true;
				}
			}
		}

		private bool LoadDB()
		{
			CheckLauncherProcess();
			if (launcherDetected || !dbLocated)
				return false;
			var conBuilder = new SqliteConnectionStringBuilder();
			conBuilder.DataSource = dbPath;
			if (playsets.Count > 0)
			{
				// drop it
				playsets.Clear();
			}
			using (var con = new SqliteConnection(conBuilder.ConnectionString))
			{
				con.Open();
				var psetSelect = con.CreateCommand();
				psetSelect.CommandText = "SELECT * from playsets";
				using (var reader = psetSelect.ExecuteReader())
				{
					while (reader.Read())
						playsets.Add(new Playset(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
				}
			}
			dbLoaded = true;
			button_pdx_import.IsEnabled = false;
			return true;
		}

		public void UpdateModList()
		{
			// todo: static list + ObservableCollection so we dont need to init list every time we swap
			if (selectedPlayset == null)
			{
				return;
			}
			List<ModSteamMeta> listOfA = new List<ModSteamMeta>();
			missingModList.Clear();
			foreach (Mod i in selectedPlayset.GetModsFromDB(dbPath))
			{
				ModSteamMeta c = JsonSerializer.Deserialize<ModSteamMeta>(JsonSerializer.Serialize(i));
				ulong steamwsid = Convert.ToUInt64(c.steamId);
				c.Installed = SWD.InstalledMods.Contains(steamwsid);
				if (!c.Installed)
				{
					missingModList.Add(steamwsid);
				}
				listOfA.Add(c);
			}
			dataGrid_Mods.ItemsSource = listOfA;
			if (missingModList.Count <= 0)
			{
				download_mods.IsEnabled = false;
			}
			else if (SWD.isConnected)
			{
				download_mods.IsEnabled = true;
				download_mods.ToolTip = "Subscribe and install all the mods from workshop.";
			}
		}

		private void ImportPlayset(string fileordata)
		{
			try
			{
				importedPlayset = null;
				string contents;
				bool isClipboard = false;
				try
				{
					contents = File.ReadAllText(fileordata);
				}
				catch
				{
					contents = fileordata;
					isClipboard = true;
				}
				importedPlayset = JsonSerializer.Deserialize<Playset>(contents);
				sps_stat.Text = "Ready to save playset. Source: " + (isClipboard ? "Clipboard" : Path.GetFileName(fileordata));
				SaveNewPlayset();
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

		private void SaveNewPlayset()
		{
			if (Process.GetProcessesByName("Paradox Launcher").Length > 0)
			{
				MessageBox.Show("Close the Paradox Launcher before saving.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			// validate the playset
			if (importedPlayset == null || importedPlayset.Name == null || importedPlayset.Id == null || importedPlayset.Name.Count() <= 0 || importedPlayset.Id.Count() <= 0)
			{
				sps_stat.Text = "Import failed due to invalid JSON";
				string err = (importedPlayset.Name == null && importedPlayset.Id == null) ? "Name and Id is null" : (importedPlayset.Name == null ? "Name is null" : (importedPlayset.Id == null ? "Id is null" : "Something **really** bad happened that we dont know."));
				MessageBox.Show("Invalid playset imported. " + err, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				importedPlayset = null; // no.
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
					pInsert.CommandText = "INSERT INTO playsets VALUES(@id, @name, 0, 'custom')";
					pInsert.Parameters.Add("@id", SqliteType.Text).Value = importedPlayset.Id;
					pInsert.Parameters.Add("@name", SqliteType.Text).Value = importedPlayset.Name;
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

				pdx_db_stat.Text = "Playset saved";
				LoadDB(); // reload it
				if (missingMods.Count > 0)
				{
					if (!SWD.isConnected)
					{
						MessageBox.Show("You might be missing some mods, we tried to contacting steam but failed! Please install it manualy. Your default text editor will open with links to the mods you must subscribe to.", "Missing mod(s)");
						string text = "Missing Mod List" + Environment.NewLine;
						text += "Please make sure you subscribe to each BEFORE starting Stellaris. If you are already subscribed but you haven't opened the launcher since you have subscribed to the listed mods then you can ignore this message.";
						foreach (Mod m in missingMods)
						{
							text += string.Format("{2}{0} - https://steamcommunity.com/sharedfiles/filedetails/?id={1}",
								m.DisplayName, m.steamId, Environment.NewLine);
						}
						File.WriteAllText("missingmods.txt", text);
						ProcessStartInfo psi = new ProcessStartInfo(Environment.CurrentDirectory + Path.DirectorySeparatorChar + "missingmods.txt");
						psi.UseShellExecute = true;
						Process.Start(psi);
						return;
					}
					missingModList.Clear();
					foreach (Mod missing in missingMods)
					{
						missingModList.Add(Convert.ToUInt64(missing.steamId));
					}
					_ = SWD.InstallMissingModsAsync(missingModList, this, popupWindow);
				}
			}
		}

		// steam linking because default doesnt do it
		private void DG_Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			Hyperlink link = (Hyperlink)e.OriginalSource;
			ProcessStartInfo psi = new ProcessStartInfo(link.NavigateUri.AbsoluteUri);
			psi.UseShellExecute = true;
			Process.Start(psi);
		}

		private void mainWindow_Activated(object sender, EventArgs e)
		{
			CheckLauncherProcess();
		}

		private void mainWindow_Deactivated(object sender, EventArgs e)
		{
			CheckLauncherProcess();
		}

		private void mainWindow_Close(object sender, EventArgs e)
		{
			SWD.Shutdown();
			popupWindow.mytimetodie = true;
			popupWindow.Close();
			Application.Current.Shutdown();
		}

		private void button_pdx_import_Click(object sender, RoutedEventArgs e)
		{
			LoadDB();
			dataGrid_Playsets.ItemsSource = playsets;
			pdx_db_stat.Text = "Loaded launcher data";
			button_SPST_import.IsEnabled = true;
			button_SPST_import_clipboard.IsEnabled = true;
		}

		private void dataGrid_Playsets_RowStateChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count != 1)
			{
				button_SPST_export.IsEnabled = false;
				button_SPST_export_clipboard.IsEnabled = false;
				sps_stat.Text = "Nothing selected.";
				return;
			}
			button_SPST_export.IsEnabled = true;
			button_SPST_export_clipboard.IsEnabled = true;
			selectedPlayset = (Playset)e.AddedItems[0];
			sps_stat.Text = "Selected: " + selectedPlayset.Name.ToString(); // overkill?
			UpdateModList();
		}

		private void refresh_modinstalled_Click(object sender, RoutedEventArgs e)
		{
			if (!SWD.isConnected)
			{
				return;
			}
			SWD.fetchInsalledMods();
			UpdateModList();
		}

		private void button_download_mods_Click(object sender, RoutedEventArgs e)
		{
			if (missingModList.Count <= 0)
			{
				return;
			}
			_ = SWD.InstallMissingModsAsync(missingModList, this, popupWindow);
		}

		/*
		 * Export methods
		 */

		private void button_SPST_export_Click(object sender, RoutedEventArgs e)
		{
			selectedPlayset.GetModsFromDB(dbPath);
			selectedPlayset.Export();
			sps_stat.Text = "Saved!";
		}

		private void button_SPST_export_clipboard_Click(object sender, RoutedEventArgs e)
		{
			selectedPlayset.GetModsFromDB(dbPath);
			selectedPlayset.ExportCliboard();
			sps_stat.Text = "Exported to clipboard!";
		}

		/*
		 * Import methods
		 */

		private void btnImport_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				// Note that you can have more than one file.
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Length > 1 || !files[0].EndsWith(".json"))
				{
					sps_stat.Text = "No.";
					return;
				}
				ImportPlayset(files[0]);
			}
		}

		private void btnImport_DragLeave(object sender, DragEventArgs e)
		{
			sps_stat.Text = "Import playset (or drag one here)";
		}

		private void btnImport_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				// Note that you can have more than one file.
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Length > 1)
				{
					sps_stat.Text = "Too many files selected!";
					return;
				}
				if (!files[0].EndsWith(".json"))
				{
					sps_stat.Text = "Only .json files accepted!";
					return;
				}
				sps_stat.Text = "Drop file to import";
			}
		}

		private void button_SPST_import_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Title = "Import playset";
			ofd.DefaultExt = ".json";
			ofd.Filter = "JSON (*.json)|*.json";

			bool? result = ofd.ShowDialog();
			if (result == true)
			{
				string file = ofd.FileName;
				if (!File.Exists(file))
					return;
				button_SPST_import.IsEnabled = false;
				button_SPST_import_clipboard.IsEnabled = false;
				ImportPlayset(file);
				button_SPST_import.IsEnabled = true;
				button_SPST_import_clipboard.IsEnabled = true;
			}
		}

		private void button_SPST_import_clipboard_Click(object sender, RoutedEventArgs e)
		{
			if (Clipboard.GetText().Length <= 0)
			{
				return;
			}
			button_SPST_import.IsEnabled = false;
			button_SPST_import_clipboard.IsEnabled = false;
			ImportPlayset(Clipboard.GetText());
			button_SPST_import.IsEnabled = true;
			button_SPST_import_clipboard.IsEnabled = true;
		}

		private void button_pdx_launch_Click(object sender, RoutedEventArgs e)
		{
			if (!SWD.isConnected)
			{
				return;
			}
			Close();
			// stellaris is x64 only, so we only need to check for this
			string config64path = null;
			RegistryKey key64 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Valve\\");
			foreach (string k32subKey in key64.GetSubKeyNames())
			{
				using (RegistryKey subKey = key64.OpenSubKey(k32subKey))
				{
					config64path = subKey.GetValue("InstallPath").ToString() + "/steamapps/common/Stellaris/dowser.exe";
					if (File.Exists(config64path))
					{
						break; // finish the loop, we got what we need
					}
				}
			}
			if(config64path != null)
            {
				new Thread(() =>
				{
					ProcessStartInfo psi = new ProcessStartInfo(config64path);
					psi.UseShellExecute = true;
					Process.Start(psi);
				}).Start();
			}
		}
	}
	public class AttatchSteamLink : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new Uri("https://steamcommunity.com/sharedfiles/filedetails/?id=" + value.ToString());
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
