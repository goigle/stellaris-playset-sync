using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace StellarisPlaysetSync
{
    public class Playset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LoadOrder { get; set; } = "custom";
        public bool isActive { get; set; } = false;
        public List<Mod> Mods { get; set; }

        // Constructor for json serialization
        public Playset() { }
        // Actual constructor
        public Playset(string id, string name, bool isactive, string loadorder = "custom")
        {
            Id = id;
            Name = name;
            isActive = isactive;
            LoadOrder = loadorder;
        }

        public List<Mod> GetModsFromDB(string dbPath)
        {
            if (Mods != null)
            {
                return Mods;
            }
            Mods = new List<Mod>();
            var conBuilder = new SqliteConnectionStringBuilder();
            conBuilder.DataSource = dbPath;
            using (var con = new SqliteConnection(conBuilder.ConnectionString))
            {
                con.Open();
                List<PlaysetMod> psetMods = new List<PlaysetMod>();
                List<string> psetModIds = new List<string>();

                // first, we get the playset mods (only inludes bare minimum data)
                var psetSelect = con.CreateCommand();
                psetSelect.CommandText = "SELECT * FROM playsets_mods WHERE playsetId=@psid";
                psetSelect.Parameters.Add("@psid", SqliteType.Text).Value = Id;
                using (var reader = psetSelect.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        PlaysetMod pm = new PlaysetMod();
                        pm.PlaysetId = reader.GetString(0);
                        if (pm.PlaysetId != Id)
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

                // now we get the actual metadata of the mods
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
                            Mods.Add(m);
                        }
                    }
                }
            }
            return Mods; 
        }

        public void Export()
        {
            SaveFileDialog save = new SaveFileDialog();
            save.Title = "Export playset";
            save.DefaultExt = ".json";
            save.AddExtension = true;
            save.FileName = Name + ".json";
            save.Filter = "JSON (*.json)|*.json";

            bool? result = save.ShowDialog();

            if (result == true)
            {
                string file = save.FileName;
                string json = JsonSerializer.Serialize(this);
                File.WriteAllText(file, json);
            }
        }
        public void ExportCliboard()
        {
            // yes i know setfile thingy works but you have to make a tmp of it to be copied as a real file
            Clipboard.SetText(JsonSerializer.Serialize(this));
        }
    }
}
