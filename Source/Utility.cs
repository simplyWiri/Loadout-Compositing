using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    public static class Utility
    {
        public static List<ThingDef> apparelDefs = null;
        public static List<ThingDef> meleeWeapons = null;
        public static List<ThingDef> rangedWeapons = null;
        public static List<ThingDef> medicinalDefs = null;
        public static List<ThingDef> items = null;

        public static void CalculateDefLists()
        {
            items ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>!( 
                !td.EverHaulable
                || td.IsFrame
                || td.destroyOnDrop
                || !td.PlayerAcquirable
                || typeof(UnfinishedThing).IsAssignableFrom(td.thingClass)
                || typeof(MinifiedThing).IsAssignableFrom(td.thingClass)
                || td.IsCorpse)).ToList();
            
            apparelDefs ??= items.Where(def => def.IsApparel).ToList();
            meleeWeapons ??= items.Where(def => def.IsMeleeWeapon).ToList();
            rangedWeapons ??= items.Where(def => def.IsRangedWeapon && def.category != ThingCategory.Building).ToList();
            medicinalDefs ??= items.Where(def => def.IsMedicine || def.IsDrug).ToList();
            
            items = items
                .Except(apparelDefs)
                .Except(meleeWeapons)
                .Except(rangedWeapons)
                .Except(medicinalDefs)
                .ToList();
        }
        
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

        public static float HypotheticalEncumberancePercent(Pawn p, List<Item> items)
        {
            return Mathf.Clamp01(HypotheticalUnboundedEncumberancePercent(p, items));
        }

        public static float HypotheticalUnboundedEncumberancePercent(Pawn p, List<Item> items)
        {
            return HypotheticalGearAndInventoryMass(p, items) / MassUtility.Capacity(p);
        }
        
        public static float HypotheticalGearAndInventoryMass(Pawn p, List<Item> items)
        {
            float mass = 0f;
            foreach (var item in items) {
                var thing = item.MakeDummyThingNoId();
                mass += (thing.GetStatValue(StatDefOf.Mass) * item.Quantity);
            }
            return mass;
        }

        public static bool IsValidLoadoutHolder(this Pawn pawn)
        {
            return pawn.RaceProps.Humanlike 
                   && pawn.IsColonist 
                   && !pawn.IsQuestLodger() 
                   && !(pawn.apparel?.AnyApparelLocked ?? true);
        }

        public static IEnumerable<Thing> InventoryAndEquipment(this Pawn pawn)
        {
            return pawn.inventory.innerContainer.InnerListForReading
                .ConcatIfNotNull(pawn.equipment.AllEquipmentListForReading);
        }
        
    }
}