using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Dialog_TagSelector : Window {
        public const int WIDTH = 320;
        public const int HEIGHT = 320;
        public static int x = -1;
        public static int y = -1;

        public override Vector2 InitialSize => new Vector2(WIDTH, HEIGHT);

        private List<Tag> tags;
        private List<Tag> selectedTags;
        private Action<Tag> onSelect = null;
        private Vector2 scrollPos = Vector2.zero;
        private string searchString = string.Empty;
        private bool multiSelect;
        private string customTitleSuffix;

        public Dialog_TagSelector(List<Tag> tags, Action<Tag> onSelect, bool multiSelect = true, string customTitleSuffix = null) {
            this.tags = tags.OrderBy(t => t.name).ToList();
            this.onSelect = onSelect;
            this.multiSelect = multiSelect;
            this.customTitleSuffix = customTitleSuffix;
            this.selectedTags = new List<Tag>();

            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            resizeable = false;
            draggable = true;
        }

        private void CloseAndSelect(Tag tag = null) {
            foreach (var t in selectedTags) {
                onSelect(t);
            }
            if ( tag != null ) {
                onSelect(tag);
            }
            
            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagSelector));
        }
        
        public override void DoWindowContents(Rect inRect) {
            var title = multiSelect ? Strings.SelectTags : Strings.SelectTag;
            if (customTitleSuffix != null) {
                title += " " + customTitleSuffix;
            }
            
            GUIUtility.ListSeperator(ref inRect, title);

            DrawSearchBar(inRect.PopTopPartPixels(UIC.SPACED_HEIGHT));
            inRect.AdjVertBy(UIC.SMALL_GAP);
            DrawTagList(inRect.PopTopPartPixels(HEIGHT - (Margin * 2 + UIC.SPACED_HEIGHT * 2 + UIC.LIST_SEP_HEIGHT + UIC.SMALL_GAP * 2)));
            inRect.AdjVertBy(UIC.SMALL_GAP);
            DrawCreateNewTag(inRect);
        }

        public void DrawSearchBar(Rect rect) {
            GUIUtility.InputField(rect, "SearchForTags", ref searchString);
        }

        public void DrawTagList(Rect rect) {
            if (tags.NullOrEmpty()) return;

            var width = rect.width - (tags.Count * UIC.SPACED_HEIGHT > rect.height ? UIC.SCROLL_WIDTH : 0);
            var viewRect = new Rect(rect.x, rect.y, width, tags.Count * UIC.SPACED_HEIGHT);
            
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

            var nameWidth = Mathf.Min(rect.width / 2.0f, tags.MaxBy(tag => tag.name.GetWidthCached()).name.GetWidthCached());
            var colorIdx = 0;

            foreach (var tag in tags) {
                if (!tag.name.Contains(searchString)) {
                    continue;
                }
                
                var tagRect = viewRect.PopTopPartPixels(UIC.SPACED_HEIGHT);

                // Draw alternating pattern
                if (colorIdx++ % 2 == 0) {
                    Widgets.DrawLightHighlight(tagRect);
                }

                var prefix = string.Empty;
                if (multiSelect && selectedTags.Contains(tag)) {
                    prefix = "* ".Colorize(Color.red);
                }
                
                // Draw highlight on hover
                if (Mouse.IsOver(tagRect)) {
                    Widgets.DrawHighlight(tagRect);
                }

                // Should it even have an edit functionality?
                var editRect = tagRect.PopRightPartPixels(UIC.SMALL_ICON);
                
                if (Widgets.ButtonImageFitted(editRect, Textures.EditTex)) {
                    Find.WindowStack.Add(new Dialog_TagEditor(tag));
                }

                // Onclick functionality
                if (Widgets.ButtonInvisible(tagRect)) {
                    if (multiSelect && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
                        if (selectedTags.Contains(tag)) {
                            selectedTags.Remove(tag);
                        } else {
                            selectedTags.Add(tag);
                        }
                    } else {
                        CloseAndSelect(tag);
                        Widgets.EndScrollView();
                        return;
                    }
                }

                // Draw tag name
                if (prefix != "") {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    
                    Widgets.Label(tagRect.PopLeftPartPixels(prefix.GetWidthCached()), prefix);

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                
                var tagNameWidth = Mathf.Max(tag.name.GetWidthCached(), nameWidth); 
                var nameRect = tagRect.PopLeftPartPixels(tagNameWidth + 10f);
                Widgets.Label(nameRect, tag.name);
                
                // arbitrary offset between names and icons
                tagRect.AdjHorzBy(UIC.SMALL_GAP);

                var iconsCanDraw = Mathf.FloorToInt(tagRect.width / UIC.SMALL_ICON);
                var numIcons = Mathf.Min(tag.requiredItems.Count, iconsCanDraw);
                
                for (var i = 0; i < numIcons; i++) {
                    Widgets.DefIcon(tagRect.PopLeftPartPixels(UIC.SMALL_ICON), tag.requiredItems[i].Def, tag.requiredItems[i].RandomStuff);
                }
            }

            Widgets.EndScrollView();
        }

        public void DrawCreateNewTag(Rect rect) {
            if (selectedTags.Any()) {
                var edgeLength = Mathf.CeilToInt(rect.width / 24.0f);
                rect.PopLeftPartPixels(edgeLength);
                rect.PopRightPartPixels(edgeLength);

                var lPart = rect.LeftPart(0.55f);
                if (Widgets.ButtonText(lPart, Strings.CreateNewTag)) {
                    var newTag = new Tag(string.Empty);
                    LoadoutManager.AddTag(newTag);
                    CloseAndSelect(newTag);
                    Find.WindowStack.Add(new Dialog_TagEditor(newTag));
                }
                
                var rPart = rect.RightPart(0.40f);
                if (Widgets.ButtonText(rPart, Strings.SelectTags)) {
                    CloseAndSelect();
                }
            }
            else {
               var edgeLength = Mathf.CeilToInt(rect.width / 6.0f);
               rect.PopLeftPartPixels(edgeLength);
               rect.PopRightPartPixels(edgeLength);

               if (Widgets.ButtonText(rect, Strings.CreateNewTag)) {
                   // in the case `searchString` is empty, it will default generate a name like `Placeholder-XXX`
                   var newTag = new Tag(searchString);
                   LoadoutManager.AddTag(newTag);
                   CloseAndSelect(newTag);
                   Find.WindowStack.Add(new Dialog_TagEditor(newTag));
               }
            }
        }

    }

}