using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory {
    public class Command_RemoveTag: Command {
        public Command_RemoveTag() {
            defaultLabel = Strings.CommandRemoveTagLabel;
            icon = Textures.PlaceholderDef;
            defaultDesc = Strings.CommandRemoveTagDesc;
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            var validTags = ListOfTagsForSelected;
            if (validTags.Count == 0) return;

            List<FloatMenuOption> list = new List<FloatMenuOption>();
            validTags.ForEach(tag => {
                list.Add(new FloatMenuOption(tag.name, delegate {
                    RemoveTagsFromSelected(tag);
                }));
            });
            Find.WindowStack.Add(new FloatMenu(list));
        }

        public List<Tag> ListOfTagsForSelected {
            get {
                var resultSet = new HashSet<Tag>();
                Find.Selector.SelectedObjects.ForEach(selected => {
                    if (selected is not Pawn) return;
                    var pawn = (Pawn)selected;
                    if (!pawn.IsValidLoadoutHolder()) return;
                    var loadoutComponent = pawn.TryGetComp<LoadoutComponent>();
                    
                    resultSet.UnionWith(loadoutComponent.Tags);
                });
                return resultSet.ToList();
            }
        }

        public void RemoveTagsFromSelected(Tag tag) {
            Find.Selector.SelectedObjects.ForEach(selected => {
                if (selected is not Pawn) return;
                var pawn = (Pawn)selected;
                if (!pawn.IsValidLoadoutHolder()) return;
                var loadoutComponent = pawn.TryGetComp<LoadoutComponent>();
                if (!loadoutComponent.HasTag(tag)) return;
                
                loadoutComponent.RemoveTag(tag);
            });
        }
    }
}
