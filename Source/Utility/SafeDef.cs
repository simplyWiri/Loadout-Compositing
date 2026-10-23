using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class SafeDef 
    {
        internal ThingDef def;
        internal string defName;
        internal bool hasLoaded = false;

        public ThingDef Def => hasLoaded ? def : Load();
        public bool Valid => hasLoaded ? def != null : Load() != null;

        public static implicit operator ThingDef(SafeDef d) => d.Def;

        public SafeDef()
        {
            this.def = null;
            this.defName = null;
        }

        public SafeDef(ThingDef def)
        {
            this.def = def;
            this.defName = def.defName;
        }
        public SafeDef(string defName)
        {
            this.def = null;
            this.defName = defName;
        }

        // Settings (tags saved for other games) load before the defs do, so a def which isn't found yet is looked up
        // again later, rather than remembered as missing.
        public ThingDef Load()
        {
            DefDatabase<ThingDef>.defsByName.TryGetValue(defName, out def);
            hasLoaded = def != null || PlayDataLoader.Loaded;
            return def;
        }

        public override string ToString()
        {
            return def?.defName ?? defName;
        }

        private string Name => def?.defName ?? defName;

        // by name, so they can be compared (e.g. stuffs in a set) before the defs have loaded, without every
        // unloaded def comparing equal
        public override bool Equals(object obj)
        {
            return obj is SafeDef safeDef && Name == safeDef.Name;
        }
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        public static SafeDef FromString(string str) {
            return new SafeDef(str);
        }
    }
}
