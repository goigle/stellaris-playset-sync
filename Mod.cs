using System;
using System.Collections.Generic;
using System.Text;

namespace StellarisPlaysetSync
{
    public class Mod
    {
        // Status types: to_install, ready_to_play
        public string Id { get; set; }
        public string steamId { get; set; }
        public string DisplayName { get; set; }
        public string PlaysetPosition { get; set; }
        public bool Enabled { get; set; }

        public Mod()
        {

        }
        public Mod(string id, string steamid, string dname, string position, bool enabled)
        {
            Id = id;
            steamId = steamid;
            DisplayName = dname;
            PlaysetPosition = position;
            Enabled = enabled;
        }

        public string getGameRegistryId()
        {
            return "mod/ugc_" + steamId + ".mod";
        }

    }
}
