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
        private Action<Tag> onSelect = null;
        private Vector2 scrollPos = Vector2.zero;
        private string searchString = string.Empty;

        public Dialog_TagSelector(List<Tag> tags, Action<Tag> onSelect) {
            this.tags = tags.OrderBy(t => t.name).ToList();
            this.onSelect = onSelect;

            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            resizeable = false;
            draggable = true;
        }

        public void CloseAndSelect(Tag tag) {
            onSelect(tag);
            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagSelector));
        }
        
        
        public override void DoWindowContents(Rect inRect) {
            // Select Tag
            // [        ] 
            // Tag 1 [ ic1, ic2, ic3, ic4 ] [ S ]
            // Tag 2 ....                   [ S ]
            // Tag n ....                   [ S ]
            //      [ Create New Tag ]
            
            GUIUtility.ListSeperator(ref inRect, Strings.SelectTag);

            DrawSearchBar(inRect.PopTopPartPixels(UIC.SPACED_HEIGHT));
            inRect.AdjVertBy(UIC.SMALL_GAP);
            DrawTagList(inRect.PopTopPartPixels(HEIGHT - (Margin * 2 + UIC.SPACED_HEIGHT * 2 + UIC.LIST_SEP_HEIGHT + UIC.SMALL_GAP)));
            DrawCreateNewTag(inRect);
        }

        public void DrawSearchBar(Rect rect) {
            GUIUtility.InputField(rect, "SearchForTags", ref searchString);
        }

        public void DrawTagList(Rect rect) {
            if (tags.NullOrEmpty()) return;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tags.Count * UIC.SPACED_HEIGHT);
            
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
                    CloseAndSelect(tag);
                    Widgets.EndScrollView();
                    return;
                }

                // Draw tag name
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
            var edgeLength = Mathf.CeilToInt(rect.width / 6.0f);
            rect.PopLeftPartPixels(edgeLength);
            rect.PopRightPartPixels(edgeLength);

            if (Widgets.ButtonText(rect, Strings.CreateNewTag)) {
                var newTag = new Tag(string.Empty);
                LoadoutManager.AddTag(newTag);
                CloseAndSelect(newTag);
                Find.WindowStack.Add(new Dialog_TagEditor(newTag));
            }
        }

    }

}