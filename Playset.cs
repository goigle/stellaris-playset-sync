using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.Text.Json;
using System.IO;

namespace StellarisPlaysetSync
{
    public class Playset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Mod> Mods { get; set; }

        public Playset()
        {

        }

        public Playset(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public void Export()
        {
            SaveFileDialog save = new SaveFileDialog();
            save.Title = "Export playset";
            save.DefaultExt = ".json";
            save.FileName = Name + ".json";
            save.Filter = "JSON (.json)|*.json";

            bool? result = save.ShowDialog();

            if (result == true)
            {
                string file = save.FileName;
                string json = JsonSerializer.Serialize(this);
                File.WriteAllText(file, json);
            } 
        }
    }
}
