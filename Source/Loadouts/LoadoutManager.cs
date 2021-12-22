using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using RimWorld.Planet;
using Verse;

namespace Inventory {

    // Loadout manager is responsible for deep saving all tags and bill extra data
    public class LoadoutManager : GameComponent {

        public static LoadoutManager instance = null;

        // Saving 
        private int nextTagId;
        private List<SerializablePawnList> pPawnLoading = null;
        private List<Tag> pTagsLoading = null;

        // pseudo-static lists.
        private List<Tag> tags = new List<Tag>();
        private Dictionary<Tag, SerializablePawnList> pawnTags = new Dictionary<Tag, SerializablePawnList>();
        private Dictionary<Bill_Production, Tag> billToTag = new Dictionary<Bill_Production, Tag>();

        public static List<Tag> Tags => instance.tags;
        public static Dictionary<Tag, SerializablePawnList> PawnsWithTags => instance.pawnTags;

        public static int GetNextTagId() => UniqueIDsManager.GetNextID(ref instance.nextTagId);

        public static Tag TagFor(Bill_Production billProduction) {
            if (instance.billToTag.TryGetValue(billProduction, out var tag)) return tag;
            return null;
        }

        public static void SetTagForBill(Bill_Production bill, Tag tag) {
            instance.billToTag.SetOrAdd(bill, tag);
        }

        public static int ColonistCountFor(Bill_Production bill) {
            // if the extra data has not been set (just switched to the bill, this is a valid
            // state to be in).
            var tag = TagFor(bill);
            if (tag == null) return 0;

            instance.pawnTags[tag].pawns.RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder());

            return instance.pawnTags[tag].Pawns.Count(p => p.Map == bill.Map);
        }

        public LoadoutManager(Game game) { }

        public static void AddTag(Tag tag) {
            Tags.Add(tag);
            PawnsWithTags.Add(tag, new SerializablePawnList(new List<Pawn>()));
        }

        public static void RemoveTag(Tag tag) {
            Tags.Remove(tag);
            if (PawnsWithTags.ContainsKey(tag))
                PawnsWithTags.Remove(tag);

            instance.billToTag.RemoveAll(bill => bill.Value == tag);

            foreach (var pawn in Find.Maps.SelectMany(map => map.mapPawns.FreeColonists)
                         .Where(p => !p.IsQuestLodger() && p.TryGetComp<LoadoutComponent>() != null)) {
                var loadout = pawn.TryGetComp<LoadoutComponent>();
                if (loadout == null) continue;
                if (loadout.Loadout.tags.Contains(tag)) {
                    loadout.Loadout.tags.Remove(tag);
                }
            }
        }

        public static List<FloatMenuOption> OptionPerTag(Func<Tag, string> labelGen, Action<Tag> onClick) {
            return Tags.OrderBy(t => t.name).Select(tag => new FloatMenuOption(labelGen(tag), () => onClick(tag)))
                .ToList();
        }

        public override void FinalizeInit() {
            instance = this;
        }

        public override void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving) {
                billToTag.RemoveAll(kv => kv.Key.repeatMode != InvBillRepeatModeDefOf.W_PerTag);
                pawnTags.Do(kv => pawnTags[kv.Key].pawns.RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder()));
            }

            Scribe_Collections.Look(ref tags, nameof(tags), LookMode.Deep);
            Scribe_Collections.Look(ref pawnTags, nameof(pawnTags), LookMode.Reference, LookMode.Deep, ref pTagsLoading,
                ref pPawnLoading);
            Scribe_Collections.Look(ref billToTag, nameof(billToTag), LookMode.Reference, LookMode.Reference);
            Scribe_Values.Look(ref nextTagId, nameof(nextTagId));

            tags ??= new List<Tag>();
            pawnTags ??= new Dictionary<Tag, SerializablePawnList>();
            billToTag ??= new Dictionary<Bill_Production, Tag>();
        }

        public override void GameComponentOnGUI() {
            
            if (InvKeyBindingDefOf.CL_OpenLoadoutEditor?.KeyDownEvent ?? false) {
                if (Find.WindowStack.WindowOfType<Dialog_LoadoutEditor>() == null) {
                    var pawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
                    var loadoutHolders = pawns.Where(p => p.IsValidLoadoutHolder());
                    var pawn = loadoutHolders.FirstOrDefault();
                    if (pawn != null) {
                        Find.WindowStack.Add(new Dialog_LoadoutEditor(pawn));
                    }
                }
                else {
                    Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_LoadoutEditor));
                }
            }

            if (InvKeyBindingDefOf.CL_OpenTagEditor?.KeyDownEvent ?? false) {
                if (Find.WindowStack.WindowOfType<Dialog_TagEditor>() == null) {
                    Find.WindowStack.Add(new Dialog_TagEditor());
                }
                else {
                    Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagEditor));
                }
            }
        }

    }

}