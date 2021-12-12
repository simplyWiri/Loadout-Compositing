using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Inventory {

    // What was initially wrote as the goals of the project on
    // 6/12/2021. 

    /*  Features:
     *      Tags:
     *          - Allow you to specify things for a pawn to have in their inventory/wear
     *          - Tags can be composited
     *      Bills:
     *          - Bills can be used to target things thing 'x where x is the number of pawns with tag y)
     *      Pawn AI:
     *          - Pawns automatically equip apparel in their tags if it is available
     *          - Pawns automatically pick up weapons/tools in their tags if available
     *      GUI:
     *          - Tags can be created from the Gear menu for pawns
     *          - Tags can use extended filters (stuffable, % wear, etc)
     */

    public class ModBase : Mod {

        public static Settings settings;
        public static Harmony harmony;

        public ModBase(ModContentPack content) : base(content) {
            settings = GetSettings<Settings>();

            harmony = new Harmony("Wiri.compositableloadouts");
            harmony.PatchAll();
        }

        // public override void DoSettingsWindowContents(Rect inRect)
        // {
        //     settings.DoSettingsWindow(inRect);
        // }
        //
        // public override string SettingsCategory()
        // {
        //     return "Compositable Loadouts";
        // }

    }

}