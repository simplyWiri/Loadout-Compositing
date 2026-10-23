using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // the base type, contains the `ThingDef`s (members) which satisfy it, and the quality/hp they must meet
    public class Item : IExposable {
        internal List<ItemMember> members;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal Filter filter;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal int quantity;

        // intentionally temporary + unsaved, only accessed in `Dialog_TagEditor.cs`
        public string quantityStr;

        public Filter Filter => filter;
        public IReadOnlyList<ItemMember> Members => members;
        public int Quantity => quantity;

        // all members are either apparel, or not
        public bool IsApparel => RepresentativeDef.IsApparel;
        public IEnumerable<ThingDef> Defs => members.Select(m => m.Def);

        // the best ranked member, used when only one can be shown or used for an estimate (icons, mass, stats)
        public ItemMember RepresentativeMember => members[0];
        public ThingDef RepresentativeDef => RepresentativeMember.Def;
        public ThingDef RepresentativeStuff => RepresentativeMember.RepresentativeStuff;
        public QualityCategory RandomQuality => (QualityCategory)Mathf.FloorToInt(((int)filter.QualityRange.max + (int)filter.QualityRange.min) / 2.0f);
        public string Label => Print();

        public bool Allows(Thing thing) => MemberAllowing(thing) != null && SharedAllows(thing);

        // whether `thing` counts towards this item for `pawn` specifically, i.e. a meal they are willing to eat
        public bool AllowsFor(Pawn pawn, Thing thing) => Allows(thing) && PawnAllows(pawn, thing);

        // the checks every member shares: the quality/hp filter, and rotten meals never counting
        private bool SharedAllows(Thing thing) => Filter.Allows(thing) && !(IsMeal(thing) && IsRotten(thing));
        private static bool PawnAllows(Pawn pawn, Thing thing) => !IsMeal(thing) || pawn.WillEat(thing);

        public ItemMember MemberFor(ThingDef def) => members.FirstOrDefault(m => m.Def == def);
        public ItemMember MemberAllowing(Thing thing) => members.FirstOrDefault(m => m.Allows(thing));

        // lower is better, members may share a rank
        public int RankOf(ItemMember member) => members.IndexOf(member);
        // the rank of the member which accepts `thing`, or -1 if this item does not accept it
        public int RankOf(Thing thing) => Allows(thing) ? RankOf(MemberAllowing(thing)) : -1;

        // members grouped by rank, best first
        public IEnumerable<IGrouping<int, ItemMember>> RankedMembers => members.GroupBy(RankOf).OrderBy(g => g.Key);
        public bool SubsetOf(Bill_Production bill) => (MemberFor(bill.recipe.ProducedThingDef)?.StuffSubsetOf(bill) ?? false) && Filter.SubsetOf(bill);

        // copies the shared quality/hp, and the stuff of the member the bill produces
        public void CopyTo(Bill_Production bill) {
            var member = MemberFor(bill.recipe.ProducedThingDef);
            member.CopyTo(bill.ingredientFilter);
            bill.limitToAllowedStuff = !member.GenericStuff;

            if (Filter.SpecificQualityRange)
                bill.qualityRange = Filter.QualityRange;

            if (Filter.SpecificHitpointRange)
                bill.hpRange = Filter.HpRange;
        }

        private static bool IsMeal(Thing thing) => thing.def.ingestible?.IsMeal ?? false;

        private static bool IsRotten(Thing thing) {
            var rottable = thing.TryGetComp<CompRottable>();
            return rottable != null && rottable.Stage != RotStage.Fresh;
        }

        // Stuff? Def.LabelCap Quality-Range?
        // "One of: A, B +n", or the def's name
        public string ShortLabel {
            get {
                if (members.Count == 1) return RepresentativeDef?.LabelCap ?? RepresentativeMember.WrappedDef.defName;

                const int shownMembers = 2;
                var labels = string.Join(", ", members.Take(shownMembers).Select(m => m.Def?.LabelCap.ToString() ?? m.WrappedDef.defName));
                if (members.Count > shownMembers) labels += $" +{members.Count - shownMembers}";
                return Strings.OneOf(labels);
            }
        }

        public bool HasAlternatives => members.Count > 1;

        public string Print() {
            var stringBuilder = new StringBuilder();

            if (members.Count > 1) {
                stringBuilder.Append(ShortLabel);
            } else {
                stringBuilder.Append(RepresentativeMember.Label);
            }

            if (Defs.Any(def => def.HasComp(typeof(CompQuality)))) {
                stringBuilder.Append(" (");

                if (filter.QualityRange.min != filter.QualityRange.max)
                    stringBuilder.Append(filter.QualityRange.ToString());
                else
                    stringBuilder.Append(filter.QualityRange.min.ToString());

                stringBuilder.Append(")");
            }

            return stringBuilder.ToString();
        }

        public Item() {
            this.members = new List<ItemMember>();
            this.filter = null;
            this.quantity = 0;
        }

        public Item(ThingDef def) {
            this.members = new List<ItemMember> { new ItemMember(new SafeDef(def), new HashSet<SafeDef>()) };
            this.filter = new Filter();
            this.quantity = 1;
        }

        // one line per rank, best first, members of the same rank share a line
        public string RankedMembersDescription() {
            return string.Join("\n", RankedMembers.Select((rank, idx) => $"{idx + 1}. " + string.Join(", ", rank.Select(m => m.ListLabel))));
        }

        // a copy holding only the members (and stuffs) whose defs exist in this game, or null if none do
        public Item CopyValid() {
            var copy = new Item { quantity = quantity, filter = new Filter() };
            copy.filter.SetQualityRange(filter.QualityRange);
            copy.filter.SetHpRange(filter.HpRange);
            copy.members = members
                .Where(m => m.WrappedDef.Valid)
                .Select(m => new ItemMember(m.WrappedDef, m.AllowedStuffs.Where(stuff => stuff.Valid).ToHashSet()))
                .ToList();

            return copy.members.Count == 0 ? null : copy;
        }

        // the new alternative is the least preferred
        public void AddAlternative(ThingDef def) {
            members.Add(new ItemMember(new SafeDef(def), new HashSet<SafeDef>()));
        }

        // pastes `from`'s quality/hp, and each of its stuffs which are valid for `to`'s members
        public static Item PasteFilter(Item from, Item to) {
            var newFilter = new Filter();
            if (to.RepresentativeDef.HasComp(typeof(CompQuality)) && from.RepresentativeDef.HasComp(typeof(CompQuality))) {
                newFilter.SetQualityRange(from.Filter.QualityRange);
            }

            if (to.RepresentativeDef.useHitPoints && from.RepresentativeDef.useHitPoints) {
                newFilter.SetHpRange(from.Filter.HpRange);
            }

            to.filter = newFilter;

            var fromStuffs = from.members.SelectMany(m => m.AllowedStuffs).ToList();
            var fromMadeFromStuff = from.Defs.Any(def => def.MadeFromStuff);
            foreach (var member in to.members) {
                member.PasteStuffs(fromStuffs, fromMadeFromStuff);
            }

            return to;
        }

        // things on `pawn`'s map which count towards this item for them, through `rankMembers` (the members of one rank).
        // Each member's things are checked against that member alone, rather than searching every member for each.
        public IEnumerable<Thing> ThingsOnMap(Pawn pawn, IEnumerable<ItemMember> rankMembers) {
            foreach (var member in rankMembers) {
                var things = pawn.Map.listerThings.ThingsOfDef(member.Def);
                if (things.NullOrEmpty()) continue;
                foreach (var thing in things) {
                    if (member.Allows(thing) && SharedAllows(thing) && PawnAllows(pawn, thing)) {
                        yield return thing;
                    }
                }
            }
        }

        public void ExposeData() {
            Scribe_Collections.Look(ref members, nameof(members), LookMode.Deep);
            Scribe_Deep.Look(ref filter, nameof(filter));
            Scribe_Values.Look(ref quantity, nameof(quantity));

            // items saved before groups existed hold a single `def`, with its stuffs saved inside `filter`
            if (Scribe.mode == LoadSaveMode.LoadingVars && members == null) {
                SafeDef def = null;
                HashSet<SafeDef> stuffs = null;
                Scribe_Values.Look(ref def, nameof(def));
                if (Scribe.EnterNode(nameof(filter))) {
                    try {
                        Scribe_Collections.Look(ref stuffs, nameof(stuffs), LookMode.Value);
                    } finally {
                        Scribe.ExitNode();
                    }
                }

                members = new List<ItemMember> { new ItemMember(def, stuffs ?? new HashSet<SafeDef>()) };
            }
        }

    }

}