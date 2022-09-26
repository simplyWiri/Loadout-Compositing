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
            Widgets.Label(inGameTagsColumn.PopTopPartPixels(Text.LineHeight), "Loaded Tags");
            Widgets.Label(savedTagsColumn.PopTopPartPixels(Text.LineHeight), "Saved Tags");
            Text.Anchor = TextAnchor.UpperLeft;

            DrawTagSummaryView(ref inGameTagsColumn, loadedTags.ToList(), ref loadedTagScroll, true);
            DrawTagSummaryView(ref savedTagsColumn, savedTags.ToList(), ref savedTagScroll, false);
        }

        private void DrawTagOperations(ref Rect rect, Tag tag, List<Tag> loadedTags, List<Tag> savedTags)
        {
            var textRect = rect.PopTopPartPixels(Text.LineHeight);
            var textStr = $"Saved tag name: ";
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

            var iconRect = buttonRect.PopRightPartPixels(Mathf.Max(UIC.SPACED_HEIGHT * 3, "Load".GetWidthCached() + 5f));

            if (Widgets.ButtonText(iconRect, "Load") && !existingWithSameName) {
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    var newTag = tag.MakeCopy();
                    newTag.name = currentTagNextName;
                    newTag.requiredItems = tag.requiredItems.Where((item) => item.WrappedDef.Valid).ToList();
                    foreach(var item in newTag.requiredItems) {
                        item.Filter.ClearInvalidStuffs();
                    }
                    newTag.uniqueId = LoadoutManager.GetNextTagId();
                    loadedTags.Add(newTag);
                }, $"Load tag \"{tag.name}\" into your game as \"{currentTagNextName}\"", MakeLoadWarnings(tag)));
            }

            if (existingWithSameName) {
                showConflict = Mouse.IsOver(iconRect);

                GUI.color = Color.red;
                Widgets.Label(buttonRect, "a tag has already been loaded with that name");
                GUI.color = Color.white;
            }
        }

        private void DrawSaveButton(ref Rect rect, Tag tag, List<Tag> savedTags) {
            var buttonRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            bool existingWithSameName = savedTags.Any(t => t.name == currentTagNextName);
            GUI.color = existingWithSameName ? Color.gray : Color.white;

            var saveButtonRect = buttonRect.PopRightPartPixels(Mathf.Max(UIC.SPACED_HEIGHT * 3, "Save".GetWidthCached() + 5f));
            
            if (Widgets.ButtonText(saveButtonRect, "Save") && !existingWithSameName) {
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    var newTag = tag.MakeCopy();
                    newTag.name = currentTagNextName;
                    newTag.requiredItems = selectedTag.requiredItems.ToList();
                    newTag.uniqueId = savedTags.Count == 0 ? 0 : savedTags.MaxBy(t => t.uniqueId).uniqueId + 1;
                    savedTags.Add(newTag);
                }, $"Save tag \"{tag.name}\" under the name \"{currentTagNextName}\"", MakeSaveWarnings(tag)));
            }

            if (existingWithSameName) {
                showConflict = Mouse.IsOver(saveButtonRect);

                GUI.color = Color.red;
                Widgets.Label(buttonRect, "a tag has already been saved with that name");
                GUI.color = Color.white;
            }
        }

        private void DrawDeleteSavedTagButton(ref Rect rect, Tag tag, List<Tag> savedTags) {
            var buttonRect = rect.PopRightPartPixels(UIC.SMALL_ICON);

            TooltipHandler.TipRegion(buttonRect, "Delete tag from saved tags");

            if (Widgets.ButtonImage(buttonRect, TexButton.DeleteX)) {
                Find.WindowStack.Add(new Dialog_ConfirmSettings(() => {
                    savedTags.Remove(tag);
                }, $"Remove tag \"{tag.name}\" from saved tags", new List<Warning>()));
            }
        }

        private List<Warning> MakeSaveWarnings(Tag tag) {
            var warnings = new List<Warning>();
            foreach(var item in tag.requiredItems) {
                if (!(item.Def.modContentPack?.IsCoreMod ?? true)) {
                    warnings.Add(new Warning() {
                        message = $"{item.Def.modContentPack.Name} depends on {item.Def.LabelCap}",
                        tooltip = "If this item is loaded into a save without these mods it will drop these items automatically from the tag automatically, without removing them from the saved tag.",
                        severity = Warning.Severity.Note
                    });
                }
                foreach(var defFilter in item.filter.AllowedStuffs) {
                    if (!(defFilter.Def.modContentPack?.IsCoreMod ?? true)) {
                        warnings.Add(new Warning() {
                            message = $"{item.Def.LabelCap}'s filter item {defFilter.Def.LabelCap} depends on {defFilter.Def.modContentPack.Name}",
                            tooltip = "If this item is loaded into a save without these mods it will drop these items automatically from the filter automatically, without removing them from the saved tag.",
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
                        message = $"Can not load {item.WrappedDef.defName}.",
                        tooltip = "This item will be dropped from the tag, its mod is most likely not loaded.",
                        severity = Warning.Severity.Warn
                    });
                }
                foreach (var defFilter in item.filter.AllowedStuffs)
                {
                    if (!defFilter.Valid)
                    {
                        warnings.Add(new Warning() {
                            message = $"Can not load filter item {defFilter.defName}.",
                            tooltip = "This item will be dropped from the tag, its mod is most likely not loaded.",
                            severity = Warning.Severity.Warn
                        });
                    }
                }
            }
            return warnings;
        }

        private void DrawTagSummaryView(ref Rect rect, List<Tag> tags, ref Vector2 scroll, bool load) {
            tags.SortByDescending(t => t.name);

            rect.AdjVertBy(GenUI.GapTiny);
            rect.PopRightPartPixels(UIC.SCROLL_WIDTH);

            var tagsHeight = tags.Sum(elem => UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.requiredItems.Count / 2.0f))));
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tagsHeight);

            Widgets.BeginScrollView(rect, ref scroll, viewRect);

            foreach (var tag in tags) {
                var tagHeight = UIC.SPACED_HEIGHT * Mathf.Max(1, Mathf.CeilToInt(tag.requiredItems.Count / 2.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight);

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
