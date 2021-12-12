using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class BodyPartGroup {

        // parent and def can be null if this is the Root BodyPartGroup (this class
        // creates a tree-like heriachy)
        public BodyPartGroup(BodyPartGroup parent, BodyPartGroupDef def) {
            this.parent = parent;
            this.def = def;
            this.children = new List<BodyPartGroup>();
        }

        public void AddChild(BodyPartGroup group) {
            children.Add(group);
        }

        public BodyPartGroup parent;
        public BodyPartGroupDef def;
        public List<ApparelLayerDef> layers;
        public List<BodyPartGroup> children;

        public void PopulateLayers(Dictionary<BodyPartGroupDef, HashSet<ApparelLayerDef>> dict) {
            // recursively set the `layers` field for all children
            if (def != null)
                this.layers = dict[def].ToList();

            foreach (var child in children)
                child.PopulateLayers(dict);
        }

        public IEnumerable<ApparelLayerDef> GetLayers() {
            // Recursively fetches all distinct layers which are held in this sub-tree
            return layers.Union(children.SelectMany(c => c.GetLayers())).Distinct();
        }

        private IEnumerable<BodyPartGroupDef> GetGroupParts() {
            // Recursively fetches all defs which are held in this sub-tree,
            // there is inherently going to be no duplication, because each node
            // contains a unique BodyPartGroupDef
            if (def != null)
                yield return def;

            foreach (var child in children.SelectMany(c => c.GetGroupParts()))
                yield return child;
        }

        public IEnumerable<List<BodyPartGroup>> GetCategories() {
            // These are root children.
            var lays = new List<Tuple<BodyPartGroup, HashSet<ApparelLayerDef>>>();
            var cats = new Dictionary<BodyPartGroup, int>();
            int index = 0;

            foreach (var child in children) {
                lays.Add(new Tuple<BodyPartGroup, HashSet<ApparelLayerDef>>(child, child.GetLayers().ToHashSet()));
                cats.Add(child, index++);
            }

            foreach (var layer in lays) {
                foreach (var oLayer in lays.Except(layer)) {
                    if (layer.Item2.Intersect(oLayer.Item2).Any()) {
                        cats[layer.Item1] = cats[oLayer.Item1];
                    }
                }
            }

            return cats.GroupBy(
                pair => pair.Value,
                pair => pair.Key,
                (_, parts) => parts.ToList());
        }

    }

    public static class ApparelUtility {

        private static Dictionary<BodyDef, BodyPartGroup> bodyPartGroups = new Dictionary<BodyDef, BodyPartGroup>();

        public static IEnumerable<ThingDef> ApparelCanFitOnBody(BodyDef body, List<ThingDef> wornApparel) {
            var slotsUsed = ApparelSlotMaker.Create(body, wornApparel);
            foreach (var def in Utility.apparelDefs.Where(t => t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(body, t))))
                yield return def;
        }

        private static BodyPartGroup BuildTree(List<BodyPartGroupDef> defs) {
            // Create our root node, holds no data.
            var root = new BodyPartGroup(null, null);

            foreach (var child in BuildTreeHelper(root, defs))
                root.AddChild(child);

            return root;
        }

        private static IEnumerable<BodyPartGroup> BuildTreeHelper(BodyPartGroup curParent, IEnumerable<BodyPartGroupDef> defs) {
            // We are given a parent, and a list of defs, and we aim to return a list of 
            // `BodyPartGroupDef`s in order of their `listOrder` which all are <= to their parent
            // in vanilla, I claim that we can observe that 'associated' BodyPartGroupDefs are
            // given similar `listOrder` values. For example. Upper Head > Ears > Teeth > Eyes
            // and, more notably, all associated records fall in the range 
            // [parentListOrder, parentListOrder - 9). We use this to group records by their
            // associated defs
            if (defs.EnumerableNullOrEmpty()) yield break;

            // Find the current largest def that we have in our current list, according
            // to the `listOrder`
            var curHead = defs.MaxBy(def => def.listOrder);
            while (curHead != null) {
                // We now have a potential parent, we will now check to see if it
                // has any children, remembering that children of this def have a
                // list order x in the range [x, x - 9)
                var group = new BodyPartGroup(curParent, curHead);
                var children = GatherChildren(group, defs
                    .Where(def => def.listOrder < curHead.listOrder
                                  && def.listOrder >= curHead.listOrder - 9)
                    .ToList());

                foreach (var child in children)
                    group.AddChild(child);

                // return this current sub-tree
                yield return group;

                // when defs aren't associated, there is simply another def with an arbitrary but
                // lower value than that of the def we just returned.
                var options = defs.Where(d => d.listOrder <= group.def.listOrder - 10);
                if (options.EnumerableNullOrEmpty())
                    yield break;

                curHead = options.MaxBy(def => def.listOrder);
            }
        }


        private static IEnumerable<BodyPartGroup> GatherChildren(BodyPartGroup parent, IEnumerable<BodyPartGroupDef> defs) {
            // similar functionality to `BuildTreeHelper`, however, children can not have children
            // (I.e. this tree has a enforced depth of at most 2, exluding the root)
            if (defs.EnumerableNullOrEmpty()) yield break;

            foreach (var child in defs.Select(d => new BodyPartGroup(parent, d)))
                yield return child;
        }

        public static BodyPartGroup BuildBodyDefBodyGroupDefs(BodyDef def) {
            // We find all of the `BodyPartGroupDef`s which are covered by apparel which 
            // exists in the def dictionary (not every combination of BodyPartGroupDef
            // and ApparelLayerDef will be exhausted by the apparels in the dictionary)

            var apparelDefs = Utility.apparelDefs;
            var bodyPartToLayersCovered = new Dictionary<BodyPartGroupDef, HashSet<ApparelLayerDef>>();

            foreach (var apparel in apparelDefs) {
                foreach (var bodyGroup in apparel.apparel.bodyPartGroups) {
                    if (bodyPartToLayersCovered.ContainsKey(bodyGroup)) {
                        bodyPartToLayersCovered[bodyGroup].AddRange(apparel.apparel.layers);
                    }
                    else {
                        bodyPartToLayersCovered.Add(bodyGroup, apparel.apparel.layers.ToHashSet());
                    }
                }
            }

            var bpg = BuildTree(bodyPartToLayersCovered.Keys.ToList());
            bpg.PopulateLayers(bodyPartToLayersCovered);
            return bpg;
        }

        public static BodyPartGroup GetBodyPartGroupFor(BodyDef body) {
            if (!bodyPartGroups.TryGetValue(body, out var bodyPartGroup)) {
                bodyPartGroup = BuildBodyDefBodyGroupDefs(body);
                bodyPartGroups.Add(body, bodyPartGroup);
            }

            return bodyPartGroup;
        }
        
        public static List<Tuple<Item, Tag>> WornApparelFor(BodyDef def, List<Tuple<Item, Tag, int>> apparels) {
            var wornApparels = new List<Tuple<Item, Tag>>();
            ApparelSlot wornApparelSlots = null;
            foreach (var (apparel, tag, _) in apparels.OrderBy(app => app.Item3)) {
                // can obviously wear it if we aren't wearing anything
                if (wornApparelSlots == null) {
                    wornApparelSlots = ApparelSlotMaker.Create(def, apparel.Def);
                    wornApparels.Add(new Tuple<Item, Tag>(apparel, tag));
                    continue;
                }

                var apparelSlots = ApparelSlotMaker.Create(def, apparel.Def);

                if (!wornApparelSlots.Intersects(apparelSlots)) {
                    wornApparels.Add(new Tuple<Item, Tag>(apparel, tag));

                    wornApparelSlots = ApparelSlotMaker.Create(def, wornApparels.Select(kv => kv.Item1.Def));
                }
            }

            return wornApparels;
        }

    }

}