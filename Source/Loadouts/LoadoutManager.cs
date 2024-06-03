﻿using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    // Loadout manager is responsible for deep saving all tags and bill extra data
    public class LoadoutManager : GameComponent {

        public const int CURRENT_BACK_COMPAT_VERSION = 1;
        public static LoadoutManager instance = null;

        // Saving 
        private int backCompat = CURRENT_BACK_COMPAT_VERSION;
        private int nextTagId;
        private int nextStateId;
#if VERSION_1_4
        private List<SerializablePawnList> pPawnLoading = null;
#elif VERSION_1_5
        private List<List<Pawn>> pPawnLoading = null;
#endif
        private List<Tag> pTagsLoading = null;

        // pseudo-static lists.
        private List<Tag> tags = new List<Tag>();
        private List<LoadoutState> states = new List<LoadoutState>();
#if VERSION_1_4
        private Dictionary<Tag, SerializablePawnList> pawnTags = new Dictionary<Tag, SerializablePawnList>();
#elif VERSION_1_5
        private Dictionary<Tag, List<Pawn>> pawnTags = new Dictionary<Tag, List<Pawn>>();
#endif
        private Dictionary<Bill_Production, Tag> billToTag = new Dictionary<Bill_Production, Tag>();

        private LoadoutState panicState = null;
        private bool inPanicState = false;

        public static List<Tag> Tags => instance.tags;
        public static List<LoadoutState> States => instance.states;
#if VERSION_1_4
        public static Dictionary<Tag, SerializablePawnList> PawnsWithTags => instance.pawnTags;
#elif VERSION_1_5
        public static Dictionary<Tag, List<Pawn>> PawnsWithTags => instance.pawnTags;
#endif
        public static LoadoutState PanicState => instance.panicState;

        public static int GetNextTagId() => UniqueIDsManager.GetNextID(ref instance.nextTagId);
        public static int GetNextStateId() => UniqueIDsManager.GetNextID(ref instance.nextStateId);

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
#if VERSION_1_4
            instance.pawnTags[tag].pawns.RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder());

            return instance.pawnTags[tag].Pawns.Count(p => p.Map == bill.Map && p.HostFaction is null);
#elif VERSION_1_5
            instance.pawnTags[tag].RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder());

            return instance.pawnTags[tag].Count(p => p.Map == bill.Map && p.HostFaction is null);
#endif
        }

        public LoadoutManager(Game game) { }

        public static void AddTag(Tag tag) {
            instance.tags.Add(tag);
#if VERSION_1_4
            PawnsWithTags.Add(tag, new SerializablePawnList(new List<Pawn>()));
#elif VERSION_1_5
            PawnsWithTags.Add(tag, new List<Pawn>());
#endif
        }

        public static void RemoveTag(Tag tag) {
            instance.tags.Remove(tag);

            if (PawnsWithTags.ContainsKey(tag))
                PawnsWithTags.Remove(tag);

            instance.billToTag.RemoveAll((pair) => pair.Value == tag);

            foreach (var pawn in Find.Maps.SelectMany(map => map.mapPawns.FreeColonistsSpawned).Where(p => p.IsValidLoadoutHolder())) {
                var loadout = pawn.TryGetComp<LoadoutComponent>();
                if (loadout == null) continue;

                loadout.Loadout.elements.RemoveAll(elem => elem.Tag == tag);
            }
        }

        public static void RemoveState(LoadoutState state) {
            instance.states.Remove(state);
            
            foreach (var pawn in Find.Maps.SelectMany(map => map.mapPawns.FreeColonistsSpawned).Where(p => p.IsValidLoadoutHolder())) {
                var loadout = pawn.TryGetComp<LoadoutComponent>();
                if (loadout == null) continue;

                loadout.Loadout.elements.Do(elem => {
                    if (state.Equals(elem.state)) {
                        elem.state = null;
                    }
                });

                if (state.Equals(loadout.Loadout.CurrentState)) {
                    loadout.Loadout.SetState(null);
                }

                var window = Find.WindowStack.WindowOfType<Dialog_LoadoutEditor>();
                if (window != null && state.Equals(window.shownState)) {
                    window.shownState = null;
                }
            }
        }
        
        public override void FinalizeInit() {
            instance = this;

            // Ugly workaround detailed on the type itself.
            BetterPawnControl_EmergencyToggle_Patch.TryPatch(ModBase.harmony);
        }

        public override void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving) {
                billToTag?.RemoveAll(kv => kv.Key.InvalidBill() || kv.Key.repeatMode != InvBillRepeatModeDefOf.W_PerTag);
#if VERSION_1_4
                pawnTags?.Do(kv => pawnTags[kv.Key]?.pawns?.RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder()));
#elif VERSION_1_5
                pawnTags?.Do(kv => pawnTags[kv.Key]?.RemoveAll(p => p is null || p.Dead || !p.IsValidLoadoutHolder()));
#endif
            }

            Scribe_Collections.Look(ref tags, nameof(tags), LookMode.Deep);
            Scribe_Collections.Look(ref states, nameof(states), LookMode.Deep);
            Scribe_Collections.Look(ref pawnTags, nameof(pawnTags), LookMode.Reference, LookMode.Deep, ref pTagsLoading, ref pPawnLoading);
            Scribe_Collections.Look(ref billToTag, nameof(billToTag), LookMode.Reference, LookMode.Reference);
            Scribe_References.Look(ref panicState, nameof(panicState));
            Scribe_Values.Look(ref inPanicState, nameof(inPanicState));
            Scribe_Values.Look(ref nextTagId, nameof(nextTagId));
            Scribe_Values.Look(ref nextStateId, nameof(nextStateId));
            Scribe_Values.Look(ref backCompat, nameof(backCompat));

            if (Scribe.mode != LoadSaveMode.Saving && backCompat != CURRENT_BACK_COMPAT_VERSION) {
                
                if (backCompat == 0 && CURRENT_BACK_COMPAT_VERSION == 1) {
                    billToTag?.Do(kv => kv.Key.repeatCount = 0);
                    backCompat = 1;
                    Log.Message("[Loadout Compositing] Successfully migrated save data from version v1.1 to v1.2");
                }
  
            }

            tags ??= new List<Tag>();
#if VERSION_1_4
            pawnTags ??= new Dictionary<Tag, SerializablePawnList>();
#elif VERSION_1_5
            pawnTags ??= new Dictionary<Tag, List<Pawn>>();
#endif
            billToTag ??= new Dictionary<Bill_Production, Tag>();
            states ??= new List<LoadoutState>();
        }

        public override void GameComponentOnGUI() {
            if (InvKeyBindingDefOf.CL_OpenLoadoutEditor?.KeyDownEvent ?? false) {
                if (Find.WindowStack.WindowOfType<Dialog_LoadoutEditor>() == null) {
                    Pawn pawn = null;

                    if (Find.Selector.SelectedPawns.Any(p => p.IsValidLoadoutHolder())) {
                        pawn = Find.Selector.SelectedPawns.First(p => p.IsValidLoadoutHolder());
                    } else {
                        var pawns = Find.Maps.SelectMany(m => m.mapPawns.AllPawns);
                        var loadoutHolders = pawns.Where(p => p.IsValidLoadoutHolder());
                        pawn = loadoutHolders.FirstOrDefault();
                    }

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

            if (InvKeyBindingDefOf.CL_PanicButton?.KeyDownEvent ?? false ) {
                TogglePanicMode();
            }
        }

        public static void SetPanicState(LoadoutState state) {
            instance.panicState = state;
        }

        public static void TogglePanicMode(bool? panicState = null) {
            if (instance.panicState == null) {
                Messages.Message(Strings.WarnNoPanicStateLoadoutSet, MessageTypeDefOf.CautionInput, false);
            }

            var targetMap = Find.CurrentMap;
            if (targetMap is null) {
                return;
            }

            var pawns = targetMap.mapPawns.FreeColonistsSpawned.Where(p => p.IsValidLoadoutHolder());
            var activatePanicState = panicState ?? !pawns.Any(p => p.TryGetComp<LoadoutComponent>().Loadout.InPanicMode);

            foreach (var pawn in pawns) {
                var comp = pawn.TryGetComp<LoadoutComponent>();
                if (activatePanicState) {
                    // No tags, don't interrupt their work.
                    if ( comp.Loadout.AllTags.Count() == 0 ) {
                        continue;
                    }

                    var currentTags = comp.Loadout.Tags;
                    var nextTags = comp.Loadout.TagsWith(instance.panicState);
                    // Activating this state wouldn't change anything for this pawn, don't interrupt their work.
                    if ( nextTags.All(currentTags.Contains) ) {
                        continue;
                    }

                    comp.Loadout.ActivatePanicMode(instance.panicState);

                    if (pawn.jobs.IsCurrentJobPlayerInterruptible()) {
                        pawn.jobs.ClearQueuedJobs();
                        pawn.jobs.CleanupCurrentJob(Verse.AI.JobCondition.InterruptForced, true);
                    }
                    instance.inPanicState = true;
                } else {
                    comp.Loadout.DeactivatePanicMode();
                    instance.inPanicState = false;
                }
            }
        }

        public static bool ActivePanicState() {
            return instance.inPanicState;
        }

    }

}