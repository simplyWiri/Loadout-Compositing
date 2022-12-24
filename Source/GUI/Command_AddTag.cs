using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory {
    public class Command_AddTag: Command {
        public Command_AddTag() {
            defaultLabel = Strings.CommandAddTagLabel;
            icon = Textures.PlaceholderDef;
            defaultDesc = Strings.CommandAddTagDesc;
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            if (LoadoutManager.Tags.Count == 0) return;

            List<FloatMenuOption> list = new List<FloatMenuOption>();
            LoadoutManager.Tags.ForEach(tag => {
                list.Add(new FloatMenuOption(tag.name, delegate {
                    AddTagsToSelected(tag);
                }));
            });
            Find.WindowStack.Add(new FloatMenu(list));
        }

        public void AddTagsToSelected(Tag tag) {
            Find.Selector.SelectedObjects.ForEach(selected => {
                if (selected is not Pawn) return;
                var pawn = (Pawn)selected;
                if (!pawn.IsValidLoadoutHolder()) return;
                var loadoutComponent = pawn.TryGetComp<LoadoutComponent>();
                if (loadoutComponent.HasTag(tag)) return;
                loadoutComponent.AddTag(tag);
            });
        }
    }
}
