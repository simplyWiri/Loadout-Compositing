using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    public class ApparelSlot {

        internal readonly BodyDef bodyDef;
        internal readonly Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>> bodyPartGroups;
        internal readonly Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>> interferingBodyPartGroups;

        private Dictionary<ApparelSlot, bool> intersectionCache;

        public ApparelSlot(BodyDef bodyDef, IEnumerable<ThingDef> apparels) {
            this.bodyDef = bodyDef;
            this.bodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.interferingBodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.intersectionCache = new Dictionary<ApparelSlot, bool>();

            foreach (var apparel in apparels) {
                foreach (var layer in apparel.apparel.layers) {
                    this.bodyPartGroups.SetOrAppend(layer, apparel.apparel.bodyPartGroups);
                    this.interferingBodyPartGroups.SetOrAppend(layer, apparel.apparel.GetInterferingBodyPartGroups(bodyDef));
                }
            }
        }

        public ApparelSlot(BodyDef bodyDef, ThingDef apparel) {
            this.bodyDef = bodyDef;
            this.bodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.interferingBodyPartGroups = new Dictionary<ApparelLayerDef, HashSet<BodyPartGroupDef>>();
            this.intersectionCache = new Dictionary<ApparelSlot, bool>();


            foreach (var layer in apparel.apparel.layers) {
                this.bodyPartGroups.SetOrAppend(layer, apparel.apparel.bodyPartGroups);
                this.interferingBodyPartGroups.SetOrAppend(layer, apparel.apparel.GetInterferingBodyPartGroups(bodyDef));
            }

            ApparelElementBuilder.MakeApparelElement(apparel, bodyDef);
        }

        private bool IntersectsWith(ApparelSlot other) {
            if (other.bodyDef != this.bodyDef) {
                Log.ErrorOnce("Trying to do intersection calculations on an a pair of ApparelSlots which refer to different body defs", 23869504);
            }

            foreach (var (key, value) in other.bodyPartGroups) {
                if (interferingBodyPartGroups.TryGetValue(key, out var bodyPartGroupDefs) && bodyPartGroupDefs.Intersect(value).Any())
                    return true;
            }

            foreach (var (key, value) in this.bodyPartGroups) {
                if (other.interferingBodyPartGroups.TryGetValue(key, out var bodyPartGroupDefs) && bodyPartGroupDefs.Intersect(value).Any())
                    return true;
            }

            return false;
        }

        public bool Intersects(ApparelSlot other) {
            if (!intersectionCache.TryGetValue(other, out var intersects)) {
                intersects = IntersectsWith(other);
                intersectionCache.Add(other, intersects);
            }

            return intersects;
        }

        public bool Intersects(BodyPartGroupDef def, ApparelLayerDef layer) {
            return bodyPartGroups.TryGetValue(layer, out var bodyPartGroupDefs) && bodyPartGroupDefs.Contains(def);
        }

    }


    public static class ApparelSlotMaker {

        private static Dictionary<KeyPair, ApparelSlot> cachedApparelSlots = new Dictionary<KeyPair, ApparelSlot>();

        public struct KeyPair {

            private BodyDef bodyDef;
            private ThingDef apparel;

            public KeyPair(BodyDef d, ThingDef a) {
                this.bodyDef = d;
                this.apparel = a;
            }

            public override int GetHashCode() {
                return (apparel.shortHash << 16) + bodyDef.shortHash;
            }

            public override bool Equals(object obj) {
                return obj is KeyPair keyPair && keyPair.apparel == apparel && keyPair.bodyDef == bodyDef;
            }

        }

        public static ApparelSlot Create(BodyDef def, ThingDef apparel) {
            var kp = new KeyPair(def, apparel);
            if (!cachedApparelSlots.TryGetValue(kp, out var slot)) {
                slot = new ApparelSlot(def, apparel);
                cachedApparelSlots.Add(kp, slot);
            }

            return slot;
        }

        // don't bother caching multi apparel ones for now
        public static ApparelSlot Create(BodyDef def, IEnumerable<ThingDef> apparel) {
            return new ApparelSlot(def, apparel);
        }

    }

}