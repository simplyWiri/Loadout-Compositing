using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class Panel_InterGameSettingsPanel {
        private Tag selectedTag = null;
        private string currentTagNextName = "";
        private bool showConflict = false;
        private bool selectedFromSavedList = false;
        private Vector2 loadedTagScroll;
        private Vector2 savedTagScroll;


        public void Draw(ref Rect rect, List<Tag> loadedTags, List<Tag> savedTags) {
            if (selectedTag != null) {
                DrawTagOperations(ref rect, selectedTag, loadedTags, savedTags);
            }

            GUI.color = Widgets.SeparatorLineColor;
            rect.AdjVertBy(UIC.SMALL_GAP);
            Widgets.DrawLineHorizontal(rect.x, rect.y - 2f, rect.width);
            rect.AdjVertBy(UIC.SMALL_GAP);
            GUI.color = Color.white;

            var inGameTagsColumn = rect.LeftHalf();
            var savedTagsColumn = rect.RightHalf();

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(inGameTagsColumn.PopTopPartPixels(Text.LineHeight), Strings.LoadedTags);
            Widgets.Label(savedTagsColumn.PopTopPartPixels(Text.LineHeight), Strings.SavedTags);
            Text.Anchor = TextAnchor.UpperLeft;

            DrawTagSummaryView(ref inGameTagsColumn, loadedTags.ToList(), ref loadedTagScroll, true);
            DrawTagSummaryView(ref savedTagsColumn, savedTags.ToList(), ref savedTagScroll, false);
        }

        private void DrawTagOperations(ref Rect rect, Tag tag, List<Tag> loadedTags, List<Tag> savedTags)
        {
            var textRect = rect.PopTopPartPixels(Text.LineHeight);
            var textStr = Strings.SavedTagName;
            var lineWidth = textStr.GetWidthCached();
            Widgets.Label(textRect.PopLeftPartPixels(lineWidth + UIC.SMALL_GAP), textStr);

            if (selectedFromSavedList) {
                DrawDeleteSavedTagButton(ref textRect, tag, savedTags);
            }
            GUIUtility.InputField(textRect, "tagNextName", ref currentTagNextName);

            if (selectedFromSavedList){
                DrawLoadButton(ref rect, tag, loadedTags);
            } else {
                DrawSaveButton(ref rect, tag, savedTags);
            }
        }

        private void DrawLoadButton(ref Rect rect, Tag tag, List<Tag> loadedTags) {
            var buttonRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            bool existingWithSameName = loadedTags.Any(t => t.name == currentTagNextName);
            GUI.color = existingWithSameName ? Color.gray : Color.white;

            var iconRect = buttonRect.PopRightPartPixels(Mathf.Max(UIC.SPACED_HEIGHT * 3, Strings.Load.GetWidthCached() + 5f));

            if (Widgets.ButtonText(iconRect, Strings.Load) && !existingWithSameName) {
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    var newTag = tag.MakeCopy();
                    newTag.name = currentTagNextName;
                    newTag.requiredItems = tag.requiredItems.Where((item) => item.WrappedDef.Valid).ToList();
                    foreach(var item in newTag.requiredItems) {
                        item.Filter.ClearInvalidStuffs();
                    }
                    newTag.uniqueId = LoadoutManager.GetNextTagId();
                    loadedTags.Add(newTag);
                }, Strings.LoadTagDialogue(tag.name, currentTagNextName), MakeLoadWarnings(tag)));
            }

            if (existingWithSameName) {
                showConflict = Mouse.IsOver(iconRect);

                GUI.color = Color.red;
                Widgets.Label(buttonRect, Strings.TagAlreadyRegistered(Strings.Loaded));
                GUI.color = Color.white;
            }
        }

        private void DrawSaveButton(ref Rect rect, Tag tag, List<Tag> savedTags) {
            var buttonRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            bool existingWithSameName = savedTags.Any(t => t.name == currentTagNextName);
            GUI.color = existingWithSameName ? Color.gray : Color.white;

            var saveButtonRect = buttonRect.PopRightPartPixels(Mathf.Max(UIC.SPACED_HEIGHT * 3, Strings.Save.GetWidthCached() + 5f));
            
            if (Widgets.ButtonText(saveButtonRect, Strings.Save) && !existingWithSameName) {
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    var newTag = tag.MakeCopy();
                    newTag.name = currentTagNextName;
                    newTag.requiredItems = selectedTag.requiredItems.ToList();
                    newTag.uniqueId = savedTags.Count == 0 ? 0 : savedTags.MaxBy(t => t.uniqueId).uniqueId + 1;
                    savedTags.Add(newTag);
                    ModBase.settings.Write();
                }, Strings.SaveTagDialogue(tag.name, currentTagNextName), MakeSaveWarnings(tag)));
            }

            if (existingWithSameName) {
                showConflict = Mouse.IsOver(saveButtonRect);

                GUI.color = Color.red;
                Widgets.Label(buttonRect, Strings.TagAlreadyRegistered(Strings.Saved));
                GUI.color = Color.white;
            }
        }

        private void DrawDeleteSavedTagButton(ref Rect rect, Tag tag, List<Tag> savedTags) {
            var buttonRect = rect.PopRightPartPixels(UIC.SMALL_ICON);

            TooltipHandler.TipRegion(buttonRect, Strings.DeleteSavedTag);
#if VERSION_1_4
            if (Widgets.ButtonImage(buttonRect, TexButton.DeleteX)) {
#elif VERSION_1_5
            if (Widgets.ButtonImage(buttonRect, TexButton.Delete)) {
#endif
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    savedTags.Remove(tag);
                    ModBase.settings.Write();
                }, Strings.ConfirmRemoveTag(tag.name), new List<Warning>()));
            }
        }

        private List<Warning> MakeSaveWarnings(Tag tag) {
            var warnings = new List<Warning>();
            foreach(var item in tag.requiredItems) {
                if (!(item.Def.modContentPack?.IsCoreMod ?? true)) {
                    warnings.Add(new Warning() {
                        message = Strings.WarningItemDependsOnModLoad(item.Def.LabelCap, item.Def.modContentPack.Name),
                        tooltip = Strings.WarningItemDependsOnModLoadDesc,
                        severity = Warning.Severity.Note
                    });
                }
                foreach(var defFilter in item.filter.AllowedStuffs) {
                    if (!(defFilter.Def.modContentPack?.IsCoreMod ?? true)) {
                        warnings.Add(new Warning() {
                            message = Strings.WarningItemFilterDependsOnModLoad(item.Def.LabelCap, defFilter.Def.LabelCap, defFilter.Def.modContentPack.Name),
                            tooltip = Strings.WarningItemFilterDependsOnModLoadDesc,
                            severity = Warning.Severity.Note
                        });
                    }
                }
            }
            return warnings;
        }

        private List<Warning> MakeLoadWarnings(Tag tag) {
            var warnings = new List<Warning>();

            foreach (var item in tag.requiredItems)
            {
                if (!item.WrappedDef.Valid)
                {
                    warnings.Add(new Warning() {
                        message = Strings.WarningItemDependsOnModSave(item.WrappedDef.defName),
                        tooltip = Strings.WarningItemDependsOnModSaveDesc,
                        severity = Warning.Severity.Warn
                    });
                }
                foreach (var defFilter in item.filter.AllowedStuffs)
                {
                    if (!defFilter.Valid)
                    {
                        warnings.Add(new Warning() {
                            message = Strings.WarningItemFilterDependsOnModSave(defFilter.defName),
                            tooltip = Strings.WarningItemFilterDependsOnModSaveDesc,
                            severity = Warning.Severity.Warn
                        });
                    }
                }
            }
            return warnings;
        }

        private void DrawTagSummaryView(ref Rect rect, List<Tag> tags, ref Vector2 scroll, bool load) {
            tags.SortBy(t => t.name);

            rect.AdjVertBy(GenUI.GapTiny);
            rect.PopRightPartPixels(UIC.SCROLL_WIDTH);

            var tagsHeight = tags.Sum(elem => UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.requiredItems.Count / 2.0f))));
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tagsHeight);

            Widgets.BeginScrollView(rect, ref scroll, viewRect);

            var frumstum = rect;
            frumstum.y += Mathf.FloorToInt(scroll.y);

            foreach (var tag in tags) {
                var tagHeight = UIC.SPACED_HEIGHT * Mathf.Max(1, Mathf.CeilToInt(tag.requiredItems.Count / 2.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight);

                if (!tagRect.Overlaps(frumstum)) {
                    continue;
                }

                if (selectedTag is not null && tag.name == selectedTag.name) {
                    if ((load == selectedFromSavedList && showConflict)) {
                        GUI.color = Color.red;
                    }
                    Widgets.DrawLightHighlight(tagRect);
                    GUI.color = Color.white;
                }

                if (Widgets.ButtonInvisible(tagRect)) {
                    if ( selectedTag?.name == tag.name && !load == selectedFromSavedList ) {
                        selectedTag = null; 
                    } else {
                        selectedFromSavedList = !load;
                        selectedTag = tag;
                        currentTagNextName = tag.name;
                    }
                }

                tagRect.AdjHorzBy(3f);
                var labelRect = tagRect.PopLeftPartPixels(100 + UIC.SMALL_GAP);
                if ( tag.name.GetWidthCached() > 100 ) {
                    var drawnName = tag.name.Substring(0, 13) + '.';
                    TooltipHandler.TipRegion(labelRect, tag.name);
                    Widgets.Label(labelRect, drawnName);
                } else {
                    Widgets.Label(labelRect, tag.name);
                }

                // draw required items in blocks of 2
                for (int i = 0; i < tag.requiredItems.Count; i += 2) {
                    for (int j = 0; j < 2; j++) {
                        var drawRect = new Rect(tagRect.x + UIC.SPACED_HEIGHT * j, tagRect.y + (i / 2.0f) * UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT);
                        if ( !drawRect.Overlaps(frumstum) ) {
                            continue;
                        }

                        var idx = i + j;
                        if (idx >= tag.requiredItems.Count) {
                            break;
                        }

                        var item = tag.requiredItems[idx];
                        if (item.Def == null) {
                            Widgets.DrawTextureFitted(drawRect, Textures.PlaceholderDef, 1.0f);
                            TooltipHandler.TipRegion(drawRect, item.WrappedDef.defName);
                        } else {
                            if (item.Quantity > 1) {
                                GUIUtility.FittedDefIconCount(drawRect, item.Def, item.RandomStuff, item.Quantity);
                            } else {
                                Widgets.DefIcon(drawRect, item.Def, item.RandomStuff);
                            }

                            TooltipHandler.TipRegion(drawRect, item.Def.LabelCap);
                        }
                    }
                }
            }

            Widgets.EndScrollView();
        }
    }
}
