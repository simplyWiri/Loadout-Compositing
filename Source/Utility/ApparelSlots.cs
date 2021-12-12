using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Inventory
{
    public class ApparelSlots
    {
        private BodyDef bodyDef;
        private Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>> bodyPartGroups;
        private Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>> interferingBodyPartGroups;

        public ApparelSlots(BodyDef def, IEnumerable<ThingDef> apparels)
        {
            this.bodyDef = def;
            this.bodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.interferingBodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            
            foreach (var apparel in apparels)
            {
                AddApparel(apparel);
            }
        }
 
        public ApparelSlots(BodyDef def, ThingDef apparel)
        {
            this.bodyDef = def;
            this.bodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.interferingBodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            
            AddApparel(apparel);
        }
        
        public ApparelSlots(BodyDef def)
        {
            this.bodyDef = def;
            this.bodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.interferingBodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            
        }
        
        public void AddApparel(ThingDef apparel)
        {
            // we do this by adding an entry per BodyPartGroupDef that the
            // apparel covers, and the layers being the layers which the
            // apparel operates on.
            foreach (var layer in apparel.apparel.layers) {
                this.bodyPartGroups.SetOrAppend(layer, apparel.apparel.bodyPartGroups);
                this.interferingBodyPartGroups.SetOrAppend(layer, apparel.apparel.GetInterferingBodyPartGroups(bodyDef));
            }

        }
        
        public bool Intersects(BodyPartGroupDef def, ApparelLayerDef layer)
        {
            return bodyPartGroups.TryGetValue(layer, out var bodyPartGroupDefs) && bodyPartGroupDefs.Contains(def);
        }
        
        public bool Intersects(ApparelSlots other)
        {
            // If two apparel slots both contain the same `BodyPartGroupDef` which
            // also has an equivalent ApparelLayerDef, they are going to clash, this
            // is equivalent to checking !ApparelUtility.CanWearTogether, but it works
            // with more than two apparel at a time.
            
            foreach (var (key, value) in other.bodyPartGroups)
            {
                if (interferingBodyPartGroups.TryGetValue(key, out var bodyPartGroupDefs) && bodyPartGroupDefs.Intersect(value).Any())
                    return true;
            }
            
            foreach (var (key, value) in this.bodyPartGroups)
            {
                if (other.interferingBodyPartGroups.TryGetValue(key, out var bodyPartGroupDefs) && bodyPartGroupDefs.Intersect(value).Any())
                    return true;
            }
            
            return false;
        }

    }
}