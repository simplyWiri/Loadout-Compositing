using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // one of the defs an `Item` accepts, along with the stuffs that def may be made from
    public class ItemMember : IExposable {
        internal SafeDef def;
        internal HashSet<SafeDef> stuffs;

        public ThingDef Def => def?.Def;
        public SafeDef WrappedDef => def;
        public HashSet<SafeDef> AllowedStuffs => stuffs;
        public bool GenericStuff => stuffs.Count == 0;

        public ThingDef RepresentativeStuff => !Def.MadeFromStuff ? null : stuffs.Any() ? stuffs.First() : GenStuff.AllowedStuffsFor(Def).First();

        public ItemMember() {
            this.def = null;
            this.stuffs = new HashSet<SafeDef>();
        }

        public ItemMember(SafeDef def, HashSet<SafeDef> stuffs) {
            this.def = def;
            this.stuffs = stuffs;
        }

        public bool Allows(Thing thing) {
            if (thing.def != Def) return false;
            if (thing.def.MadeFromStuff && !stuffs.EnumerableNullOrEmpty() && thing.Stuff is not null && !stuffs.Contains(new SafeDef(thing.Stuff))) return false;

            return true;
        }

        public void ClearInvalidStuffs() {
            stuffs = stuffs.Where(def => def.Valid).ToHashSet();
        }

        public void CopyTo(ThingFilter thingFilter) {
            thingFilter.allowedDefs = stuffs.Count == 0 ? GenStuff.AllowedStuffsFor(Def).ToHashSet() : AllowedStuffs.Select(d => d.Def).ToHashSet();
        }

        public bool StuffSubsetOf(Bill_Production other) {
            var filterStuff = other.ingredientFilter.allowedDefs;

            return stuffs.Count == 0
                ? GenStuff.AllowedStuffsFor(Def).All(t => other.ingredientFilter.Allows(t))
                : filterStuff.All(t => other.ingredientFilter.Allows(t));
        }

        // keeps the pasted stuffs which are valid for this def, or our own if none of them are
        public void PasteStuffs(IEnumerable<SafeDef> fromStuffs, bool fromMadeFromStuff) {
            if (!Def.MadeFromStuff || !fromMadeFromStuff) {
                stuffs = new HashSet<SafeDef>();
                return;
            }

            var validStuffs = GenStuff.AllowedStuffsFor(Def).ToHashSet();
            var newStuffs = fromStuffs.Where(allowedStuff => validStuffs.Contains(allowedStuff)).ToHashSet();
            if (newStuffs.Any()) {
                stuffs = newStuffs;
            }
        }

        public void ExposeData() {
            Scribe_Values.Look(ref def, nameof(def));
            Scribe_Collections.Look(ref stuffs, nameof(stuffs), LookMode.Value);
            stuffs ??= new HashSet<SafeDef>();
        }
    }

}
