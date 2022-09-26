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

        public ThingDef Load() {
            if (DefDatabase<ThingDef>.defsByName.TryGetValue(defName, out var foundDef) ){
                this.def = foundDef;
            }
            hasLoaded = true;
            return this.def;
        }

        public override string ToString()
        {
            return def?.defName ?? defName;
        }

        public override bool Equals(object obj)
        {
            if (obj is SafeDef safeDef) {
                return Def == safeDef.Def;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Def?.GetHashCode() ?? 0;
        }

        public static SafeDef FromString(string str) {
            return new SafeDef(str);
        }
    }
}
