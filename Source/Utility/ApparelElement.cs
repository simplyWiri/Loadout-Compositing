using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Inventory
{
    // Score has to be constant here.
    public class ApparelElement : ICandidateElement<ApparelElement>, IComparable<ApparelElement>
    {
        // 64 bit unsigned integers, we support 64 body parts & 64 apparel laers.
        internal ulong bodyPartGroupBitmask = 0;
        internal ulong interferingBodyPartGroupBitmask = 0;
        internal ulong apparelLayerBitmask = 0;
        internal float score = 0;

        public int CompareTo(ApparelElement other)
        {
            if (bodyPartGroupBitmask != other.bodyPartGroupBitmask) {
                return (int)(bodyPartGroupBitmask - other.bodyPartGroupBitmask);
            }
            if (interferingBodyPartGroupBitmask != other.interferingBodyPartGroupBitmask) {
                return (int)(interferingBodyPartGroupBitmask - other.interferingBodyPartGroupBitmask);
            }
            if (apparelLayerBitmask != other.apparelLayerBitmask) {
                return (int)(apparelLayerBitmask - other.apparelLayerBitmask);
            }

            return (int)(score - other.score);
        }

        // this will get called a very large amount (millions for even simple) resolutions.
        public bool ConflictsWith(ApparelElement other)
        {
            var apparelLayersIntersect = (apparelLayerBitmask ^ other.apparelLayerBitmask)
                                       != apparelLayerBitmask + other.apparelLayerBitmask;

            var bodyPartsIntersect = (interferingBodyPartGroupBitmask ^ other.bodyPartGroupBitmask) != interferingBodyPartGroupBitmask + other.bodyPartGroupBitmask
                                  || (bodyPartGroupBitmask ^ other.interferingBodyPartGroupBitmask) != bodyPartGroupBitmask + other.interferingBodyPartGroupBitmask;

            return apparelLayersIntersect && bodyPartsIntersect;
        }

        public float Score()
        {
            return score;
        }
    }

    // All bodyGroupDefs which apparel can cover in your game:
    // DefDatabase<ThingDef>.AllDefs.Where((ThingDef td) => td.IsApparel)
    //                              .SelectMany((ThingDef td) => td.apparel.bodyPartGroups)
    //                              .Distinct();

    public static class ApparelElementBuilder {
        internal static Dictionary<BodyPartGroupDef, int> groupToInteger = new Dictionary<BodyPartGroupDef, int>();
        internal static Dictionary<ApparelLayerDef, int> layerToInteger = new Dictionary<ApparelLayerDef, int>();

        static ApparelElementBuilder() {
            var relevantBodyPartDefs = DefDatabase<ThingDef>.AllDefs.Where((ThingDef td) => td.IsApparel)
                                                                    .SelectMany((ThingDef td) => td.apparel.bodyPartGroups)
                                                                    .Distinct()
                                                                    .ToList();
            
            var relevantLayers = DefDatabase<ApparelLayerDef>.AllDefs.ToList();

            if ( relevantBodyPartDefs.Count > 64 || relevantLayers.Count > 64 ) {
                Log.Error("[Loadout Compositing] Data precondition has been violated. Apparel resolving algorithm will not be correct");
                return;
            }

            Log.Message($"[Loadout Compositing] {relevantBodyPartDefs.Count} body part defs loaded, {relevantLayers.Count} apparel layers loaded");

            groupToInteger = relevantBodyPartDefs.Select((key, idx) => new { key, idx }).ToDictionary(pair => pair.key, pair => pair.idx);
            layerToInteger = relevantLayers.Select((key, idx) => new { key, idx }).ToDictionary(pair => pair.key, pair => pair.idx);
        }

        static int nextScore = 0;

        public static ApparelElement MakeApparelElement(ThingDef apparel, BodyDef body) {
            
            var element = new ApparelElement();

            var layers = apparel.apparel.layers;
            var bodyPartGroups = apparel.apparel.bodyPartGroups;
            var interferingBodyPartGroups = apparel.apparel.GetInterferingBodyPartGroups(body);

            foreach(var layer in layers ) {
                element.apparelLayerBitmask |= 1u << layerToInteger[layer];
            }
            foreach (var layer in bodyPartGroups) {
                element.bodyPartGroupBitmask |= 1u << groupToInteger[layer];
            }
            foreach (var layer in interferingBodyPartGroups) {
                element.interferingBodyPartGroupBitmask |= 1u << groupToInteger[layer];
            }

            element.score = nextScore++;

            return element;
        }

    }

}
