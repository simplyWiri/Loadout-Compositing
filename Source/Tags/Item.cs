using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // the base type, contains the `ThingDef`s (members) which satisfy it, and the quality/hp they must meet. A generic
    // item saves only its `category`, its members come from that category.
    public class Item : IExposable {
        // only used when not generic
        internal List<ItemMember> members;
        internal string category;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal Filter filter;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal int quantity;

        // intentionally temporary + unsaved, only accessed in `Dialog_TagEditor.cs`
        public string quantityStr;

        public Filter Filter => filter;
        public IReadOnlyList<ItemMember> Members => MemberList;
        public int Quantity => quantity;

        public bool IsGeneric => category != null;
        // null if not generic, or the category is unknown
        public GenericCategory Category => IsGeneric ? GenericCategory.Named(category) : null;
        private List<ItemMember> MemberList => IsGeneric ? Category?.Members ?? new List<ItemMember>() : members;

        // all members are either apparel, or not
        public bool IsApparel => RepresentativeDef.IsApparel;
        public IEnumerable<ThingDef> Defs => MemberList.Select(m => m.Def);

        // the best ranked member, used when only one can be shown or used for an estimate (icons, mass, stats)
        public ItemMember RepresentativeMember => MemberList[0];
        public ThingDef RepresentativeDef => RepresentativeMember.Def;
        public ThingDef RepresentativeStuff => RepresentativeMember.RepresentativeStuff;
        public QualityCategory RandomQuality => (QualityCategory)Mathf.FloorToInt(((int)filter.QualityRange.max + (int)filter.QualityRange.min) / 2.0f);
        public string Label => Print();

        // rotten things never count towards generic items
        public bool Allows(Thing thing) => MemberAllowing(thing) != null && Filter.Allows(thing) && !(IsGeneric && IsRotten(thing));

        // whether `thing` counts towards this item for `pawn` specifically, i.e. a meal they are willing to eat
        public bool AllowsFor(Pawn pawn, Thing thing) {
            if (!Allows(thing)) return false;
            if (Category == GenericCategory.Meals && !pawn.WillEat(thing)) return false;

            return true;
        }

        public ItemMember MemberFor(ThingDef def) => MemberList.FirstOrDefault(m => m.Def == def);
        public ItemMember MemberAllowing(Thing thing) => MemberList.FirstOrDefault(m => m.Allows(thing));

        // lower is better, members may share a rank
        public int RankOf(ItemMember member) => IsGeneric ? Category.RankOf(member.Def) : members.IndexOf(member);
        // the rank of the member which accepts `thing`, or -1 if this item does not accept it
        public int RankOf(Thing thing) => Allows(thing) ? RankOf(MemberAllowing(thing)) : -1;

        // members grouped by rank, best first
        public IEnumerable<IGrouping<int, ItemMember>> RankedMembers => MemberList.GroupBy(RankOf).OrderBy(g => g.Key);
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

        private static bool IsRotten(Thing thing) {
            var rottable = thing.TryGetComp<CompRottable>();
            return rottable != null && rottable.Stage != RotStage.Fresh;
        }

        // Stuff? Def.LabelCap Quality-Range?
        public string Print() {
            if (IsGeneric) {
                return Category?.Label ?? category;
            }

            var stringBuilder = new StringBuilder();

            var def = RepresentativeDef;
            if (def.MadeFromStuff) {
                var stuffs = RepresentativeMember.AllowedStuffs;
                var count = stuffs.Count;
                if (count == 0) stringBuilder.Append(Strings.Generic);
                else if (count == 1) stringBuilder.Append(stuffs.First().Def.LabelCap);
                else stringBuilder.Append(Strings.Generic + "*");
                stringBuilder.Append(" ");
            }

            stringBuilder.Append(def.LabelCap);
            if (def.HasComp(typeof(CompQuality))) {
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

        public Item(GenericCategory category) {
            this.members = new List<ItemMember>();
            this.category = category.key;
            // generics have no filter to edit, so accept any quality and hit points
            this.filter = new Filter();
            this.filter.SetQualityRange(QualityRange.All);
            this.filter.SetHpRange(FloatRange.ZeroToOne);
            this.quantity = 1;
        }

        // pastes `from`'s quality/hp, and each of its stuffs which are valid for `to`'s members
        public static Item PasteFilter(Item from, Item to) {
            if (to.IsGeneric || from.IsGeneric) return to;

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

        // exists to make generic `Items` easier
        public IEnumerable<Thing> ThingsOnMap(Map map) {
            foreach (var member in MemberList) {
                var things = map.listerThings.ThingsOfDef(member.Def);
                if (things.NullOrEmpty()) continue;
                foreach (var thing in things.Where(Allows)) {
                    yield return thing;
                }
            }
        }

        public void ExposeData() {
            Scribe_Values.Look(ref category, nameof(category));
            if (!IsGeneric) {
                Scribe_Collections.Look(ref members, nameof(members), LookMode.Deep);
            }
            Scribe_Deep.Look(ref filter, nameof(filter));
            Scribe_Values.Look(ref quantity, nameof(quantity));

            if (Scribe.mode == LoadSaveMode.LoadingVars && IsGeneric) {
                members = new List<ItemMember>();
            }

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