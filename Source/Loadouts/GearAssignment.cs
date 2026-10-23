using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Inventory {

    // Decides which of a pawn's held things count towards which item of their loadout. Items with fewer members
    // choose first, each taking its best ranked things first, then things fewer other items could use, then
    // equipped things, then smaller stacks. Whatever is left unassigned is surplus to the loadout.
    public class GearAssignment {
        private readonly Dictionary<Item, Dictionary<int, int>> heldByRank = new Dictionary<Item, Dictionary<int, int>>();
        private readonly Dictionary<Thing, int> unassigned;

        public GearAssignment(IEnumerable<Item> items, List<Thing> heldThings) {
            var distinctItems = items.Distinct().ToList();
            unassigned = heldThings.ToDictionary(thing => thing, thing => thing.stackCount);

            foreach (var item in distinctItems.OrderBy(item => item.Members.Count)) {
                var counts = new Dictionary<int, int>();
                var needed = item.Quantity;

                var candidates = heldThings.Where(item.Allows)
                    .OrderBy(item.RankOf)
                    .ThenBy(thing => distinctItems.Count(other => other != item && other.Allows(thing)))
                    .ThenByDescending(thing => thing.ParentHolder is Pawn_EquipmentTracker)
                    .ThenBy(thing => thing.stackCount);

                foreach (var thing in candidates) {
                    if (needed == 0) break;

                    var taken = Mathf.Min(needed, unassigned[thing]);
                    if (taken == 0) continue;

                    unassigned[thing] -= taken;
                    needed -= taken;

                    var rank = item.RankOf(thing);
                    counts[rank] = counts.TryGetValue(rank, out var count) ? count + taken : taken;
                }

                heldByRank[item] = counts;
            }
        }

        public int HeldOfRank(Item item, int rank) {
            return heldByRank.TryGetValue(item, out var counts) && counts.TryGetValue(rank, out var count) ? count : 0;
        }

        public int Surplus(Thing thing) => unassigned.TryGetValue(thing, out var count) ? count : 0;
        public IEnumerable<Thing> SurplusThings => unassigned.Where(kv => kv.Value > 0).Select(kv => kv.Key);
    }

}
