using System;
using RimWorld;
using Verse;

namespace Inventory
{
    public static class Utility
    {
        public static QualityCategory Next(this QualityCategory qc)
        {
            switch (qc)
            {
                case QualityCategory.Awful: return QualityCategory.Poor;
                case QualityCategory.Poor: return QualityCategory.Normal;
                case QualityCategory.Normal: return QualityCategory.Good;
                case QualityCategory.Good: return QualityCategory.Excellent;
                case QualityCategory.Excellent: return QualityCategory.Masterwork;
                default:
                    return QualityCategory.Legendary;
            }
        }
        public static QualityCategory Previous(this QualityCategory qc)
        {
            switch (qc)
            {
                case QualityCategory.Legendary: return QualityCategory.Masterwork;
                case QualityCategory.Masterwork: return QualityCategory.Excellent;
                case QualityCategory.Excellent: return QualityCategory.Good;
                case QualityCategory.Good: return QualityCategory.Normal;
                case QualityCategory.Normal: return QualityCategory.Poor;
                default:
                    return QualityCategory.Awful;
            }
        }

        public static Thing MakeThingWithoutID(ThingDef def, ThingDef stuff, QualityCategory quality)
        {
            Thing thing = (Thing)Activator.CreateInstance(def.thingClass);
            thing.def = def;
            if (def.MadeFromStuff)
                thing.SetStuffDirect(stuff);
            if (thing.def.useHitPoints)
                thing.HitPoints = thing.MaxHitPoints;

            if (thing is ThingWithComps thingWithComps)
                thingWithComps.InitializeComps();

            thing.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
            
            return thing;
        }
    }
}