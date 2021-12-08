using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Inventory
{
    // Loadout manager is responsible for deep saving all tags and bill extra data
    public class LoadoutManager : GameComponent
    {
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
        
        public static Tag TagFor(Bill_Production billProduction) => instance.billToTag[billProduction];
        public static int ColonistCountFor(Bill_Production bill) => instance.pawnTags[TagFor(bill)].Pawns.Count;
        
        public LoadoutManager(Game game)
        {
            
        }

        public static void AddTag(Tag tag)
        {
            Tags.Add(tag);
        }

        public static List<FloatMenuOption> OptionPerTag(Func<Tag, string> labelGen, Action<Tag> onClick)
        {
            return Tags.Select(tag => new FloatMenuOption(labelGen(tag), () => onClick(tag) )).ToList();
        }

        public override void FinalizeInit()
        {
            instance = this;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref tags, nameof(tags), LookMode.Deep);
            Scribe_Collections.Look(ref pawnTags, nameof(pawnTags), LookMode.Reference, LookMode.Deep, ref pTagsLoading, ref pPawnLoading);
            Scribe_Collections.Look(ref billToTag, nameof(billToTag), LookMode.Reference, LookMode.Reference);
            Scribe_Values.Look(ref nextTagId, nameof(nextTagId));
            
            tags ??= new List<Tag>();
            pawnTags ??= new Dictionary<Tag, SerializablePawnList>();
        }
    }
}