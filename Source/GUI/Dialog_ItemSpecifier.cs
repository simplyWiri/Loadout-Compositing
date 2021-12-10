using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HotSwappable]
    public class Dialog_ItemSpecifier : Window
    {
        private static List<StatDef> armorStats;
        private static List<StatDef> baseWeaponStats;
        private static List<StatDef> meleeWeaponStats;
        private static List<StatDef> generalItemStats;

        private ThingDef stuffPreview;
        private Filter filter;

        private QualityCategory qualityPreview = QualityCategory.Normal;

        private string stuffScrollViewFilter = String.Empty;
        private float stuffScrollViewHeight = 0;
        private Vector2 stuffScrollPosition = Vector2.zero;

        private float statScrollViewHeight = 0;
        private Vector2 statScrollPosition = Vector2.zero;

        private static Dictionary<Tuple<ThingDef, ThingDef, QualityCategory>, Dictionary<StatDef, float>> statCache;
        private static Dictionary<Tuple<ThingDef, ThingDef, QualityCategory>, Dictionary<StatDef, string>> tipCache;


        static Dialog_ItemSpecifier()
        {
            armorStats = new List<StatDef>()
            {
                StatDefOf.MaxHitPoints,
                StatDefOf.ArmorRating_Sharp,
                StatDefOf.ArmorRating_Blunt,
                StatDefOf.ArmorRating_Heat,
                StatDefOf.Insulation_Cold,
                StatDefOf.Insulation_Heat,
                StatDefOf.Mass,
            };

            baseWeaponStats = new List<StatDef>()
            {
                StatDefOf.MaxHitPoints,
                StatDefOf.MeleeWeapon_AverageDPS,
                StatDefOf.MeleeWeapon_AverageArmorPenetration,
                StatDefOf.Mass,
            };

            meleeWeaponStats = new List<StatDef>()
            {
                StatDefOf.MeleeWeapon_CooldownMultiplier,
            };
            

            generalItemStats = new List<StatDef>()
            {
                StatDefOf.MaxHitPoints,
                StatDefOf.MarketValue,
                StatDefOf.Mass,
            };

            statCache = new Dictionary<Tuple<ThingDef, ThingDef, QualityCategory>, Dictionary<StatDef, float>>();
            tipCache = new Dictionary<Tuple<ThingDef, ThingDef, QualityCategory>, Dictionary<StatDef, string>>();
        }
        
        private static StatDef ToStatDef(string str)
        {
            StatDef newDef = new StatDef()
            {
                label = str.TranslateSimple(),
                defName = str,
            };
            ShortHashGiver.GiveShortHash(newDef, typeof(StatDef));
            return newDef;
        }

        public Dialog_ItemSpecifier(Filter filter)
        {
            this.filter = filter;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(840, 640);

        public override void DoWindowContents(Rect canvas)
        {
            if (Event.current.type == EventType.Layout)
                return;

            if (filter.Thing.MadeFromStuff)
            {
                this.DoWindowContentsGeneral(canvas);
            }
            else
            {
                this.DoWindowContentsForNoStuffItem(canvas);
            }
        }
        
        protected virtual void DoWindowContentsForNoStuffItem(Rect canvas)
        {
            float rollingY = 0;

            // Draw Title
            Rect titleRec = DrawTitle(canvas.position, "Customize " + filter.Thing.LabelCap, ref rollingY);

            GUI.color = Color.grey;
            Text.Anchor = TextAnchor.LowerLeft;
            Text.WordWrap = false;
            Widgets.Label(new Rect(titleRec.x, titleRec.yMax, canvas.width, GenUI.ListSpacing),
                "(" + "Mouse Over Numbers for Details" + ")");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.color = Color.white;

            // Draw stats
            Rect statRect = new Rect(canvas.x, rollingY + GenUI.ListSpacing, canvas.width, canvas.height - rollingY - GenUI.ListSpacing * 3);
            GUIUtility.ListSeperator(ref statRect, "Statistics");
            this.DrawStats(statRect);

            // // Draw sliders
            this.DrawSliders(new Rect(statRect.x, statRect.yMax, statRect.width / 2, GenUI.SmallIconSize).CenteredOnXIn(canvas));
        }

        protected virtual void DoWindowContentsGeneral(Rect canvas)
        {
            float rollingY = 0;

            // Draw Title
            Rect titleRec = DrawTitle(canvas.position,Strings.Customize + " " + filter.Thing.LabelCap, ref rollingY);

            GUI.color = Color.grey;
            Text.Anchor = TextAnchor.LowerLeft;
            Text.WordWrap = false;
            Widgets.Label(new Rect(titleRec.x, titleRec.yMax, canvas.width, GenUI.ListSpacing), Strings.MouseOverDetails);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.color = Color.white;

            // Draw scrollable list
            canvas.yMin = rollingY + GenUI.GapSmall;
            Rect listRect = new Rect(canvas.x, canvas.y, canvas.width * 0.35f + GenUI.GapSmall,
                canvas.height - GenUI.ListSpacing);
            DrawStuffScrollableList(listRect);

            // // Draw quality choice for preview
            this.DrawPreviewQuality(new Rect(listRect.x, listRect.yMax, listRect.width, GenUI.ListSpacing));

            // Draw stats
            Rect rect = canvas.RightPart(0.65f);
            rect.yMax -= GenUI.ListSpacing * 2;
            rect.x += GenUI.GapSmall;
            GUIUtility.ListSeperator(ref rect, Strings.Statistics);
            DrawStats(rect.TopPart(0.8f));

            // Draw sliders
            this.DrawSliders(new Rect(rect.x, (rect.y + rect.height * 0.8f) + GenUI.GapTiny, rect.width * 0.6f, GenUI.SmallIconSize).CenteredOnXIn(rect));
        }

        protected virtual Rect DrawTitle(Vector2 position, string title, ref float rollingY)
        {
            Text.Font = GameFont.Medium;
            Vector2 titleSize = Text.CalcSize(title);
            Rect rectToDraw = new Rect(position, titleSize);
            Widgets.Label(rectToDraw, title);
            Text.Font = GameFont.Small;
            rollingY = rectToDraw.yMax;
            return rectToDraw;
        }
     
         protected virtual void DrawSliders(Rect rect)
         {
             // Draw hitpoint slider
             int dragID = Rand.Int;
             Rect drawRect = rect;

             if (filter.Thing.useHitPoints)
             {
                 this.DrawHitPointsSlider(drawRect, dragID);
                 drawRect = new Rect(drawRect.x, drawRect.y + drawRect.height + GenUI.GapTiny, drawRect.width, drawRect.height);
             }

             // Draw quality slider
             if (filter.Thing.HasComp(typeof(CompQuality)))
             {
                 this.DrawQualitySlider(drawRect, ++dragID);
             }
         }
         
         protected virtual void DrawQualitySlider(Rect qualityRect, int dragID)
         {
             QualityRange qualityRange = filter.QualityRange;
             Widgets.QualityRange(qualityRect, dragID, ref qualityRange);
             filter.SetQualityRange(qualityRange);
         }

         protected virtual void DrawHitPointsSlider(Rect hitpointRect, int dragID)
         {
             FloatRange hitpointRange = filter.HpRange;
             Widgets.FloatRange(hitpointRect, dragID, ref hitpointRange, 0f, 1f, Strings.HitPointsAmount, ToStringStyle.PercentZero);
             filter.SetHpRange(hitpointRange);
         }
         
        protected virtual void DrawPreviewQuality(Rect rect)
        {
            GUI.color = Color.grey;
            Text.Anchor = TextAnchor.MiddleLeft;
            var previewQualityRect = rect.PopLeftPartPixels(rect.width / 2.3f);
            Widgets.Label(previewQualityRect, Strings.PreviewQuality);
            GUI.color = Color.white;

            rect.PopRightPartPixels(GenUI.ListSpacing);

            var lRect = rect.PopLeftPartPixels(GenUI.SmallIconSize);
            var rRect = rect.PopRightPartPixels(GenUI.SmallIconSize);
            if ( Widgets.ButtonImageFitted(lRect, Textures.PlaceholderTex) )
                qualityPreview = qualityPreview.Previous();

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, qualityPreview.GetLabel());
            Text.Anchor = TextAnchor.UpperLeft;

            if ( Widgets.ButtonImageFitted(rRect, Textures.PlaceholderTex) )
                qualityPreview = qualityPreview.Next();
        }

        protected virtual void DrawStuffScrollableList(Rect canvas)
        {
            if (filter.Thing.MadeFromStuff)
            {
                canvas.width -= GenUI.GapSmall;

                // Draw stuff source.
                var stuffs = GenStuff.AllowedStuffsFor(filter.Thing).ToList();
                canvas.yMin += GenUI.ListSpacing;
                DrawStuffSourceScrollableList(canvas, stuffs, ref stuffScrollPosition, ref stuffScrollViewHeight);
            }
        }


        protected virtual void DrawStuffSourceScrollableList(Rect outRect, List<ThingDef> stuffList,
            ref Vector2 scrollPosition, ref float scrollViewHeight)
        {
            // Draw a search bar
            GUIUtility.InputField(outRect.PopTopPartPixels(GenUI.ListSpacing).ContractedBy(2f), Strings.StuffFilter,
                ref stuffScrollViewFilter);

            // filter stuff list by search bar
            stuffList.RemoveAll(td => !td.LabelCap.Resolve().Contains(stuffScrollViewFilter));

            Rect viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f, scrollViewHeight);
            Text.Font = GameFont.Small;
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            Rect row = new Rect(viewRect.x, viewRect.y, viewRect.width, GenUI.ListSpacing);
            if (!stuffList.Any())
            {
                Rect rect = outRect.TopPart(0.6f);
                Text.Font = GameFont.Medium;
                Widgets.NoneLabelCenteredVertically(rect, Strings.NoMaterial);
                Text.Font = GameFont.Small;
            }

            Text.Anchor = TextAnchor.MiddleCenter;

            stuffList.SortBy(t => t.defName);
            for (int i = 0; i < stuffList.Count; ++i)
            {
                ThingDef stuff = stuffList[i];

                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(row);

                // [ Icon ] [ Label ] [ CheckBox ] 
                var iconRect = row.LeftPartPixels(row.height);
                var checkBoxRect = row.RightPartPixels(row.height);
                var labelRect = new Rect(row.x + row.height, row.y, row.width - 2 * row.height, row.height);
                Widgets.DefIcon(iconRect, stuff);
                Widgets.Label(labelRect, stuff.LabelCap);

                var state = filter.AllowedStuffs.Contains(stuff);
                if (GUIUtility.DraggableCheckbox(row, checkBoxRect, ref state))
                {
                    if (state)
                    {
                        filter.AllowedStuffs.Add(stuff);
                    }
                    else
                    {
                        filter.AllowedStuffs.Remove(stuff);
                    }
                }

                // Set stuff for preview.
                if (Mouse.IsOver(row))
                {
                    stuffPreview = stuff;
                    Widgets.DrawHighlight(row);
                }

                row.y = row.yMax;
            }

            Text.Anchor = TextAnchor.UpperLeft;

            scrollViewHeight = stuffList.Count * GenUI.ListSpacing;
            Widgets.EndScrollView();
        }

         private void DrawStatRows(List<StatDef> stats, Rect startRect)
         {
             for (int i = 0; i < stats.Count; i++)
             {
                 var statInfoList = new List<StatDrawInfo>();

                 void DrawColumnFor(ThingDef def, ThingDef stuff, QualityCategory qual)
                 {
                     var drawInfo = new StatDrawInfo();
                     var tempThing = Utility.MakeThingWithoutID(def, stuff, qual);
                     StatRequest statRequest = StatRequest.For(tempThing);
                     if ((stats[i].Worker.ShouldShowFor(statRequest) && !stats[i].Worker.IsDisabledFor(tempThing)) || stats[i] == StatDefOf.MaxHitPoints || stats[i] == StatDefOf.MeleeWeapon_CooldownMultiplier)
                     {
                         drawInfo.StatRequest = statRequest;
                         drawInfo.Value = GetCachedValue(
                             statCache, 
                             () => stats[i].Worker.GetValue(drawInfo.StatRequest), 
                             new Tuple<ThingDef, ThingDef, QualityCategory>(def, stuff, qual), 
                             stats[i]);
                         drawInfo.Tip = GetCachedValue(
                             tipCache, 
                             () => stats[i].Worker.GetExplanationFull(drawInfo.StatRequest, stats[i].toStringNumberSense, drawInfo.Value), 
                             new Tuple<ThingDef, ThingDef, QualityCategory>(def, stuff, qual), 
                             stats[i]);
                     }
                     else
                     {
                         drawInfo.Value = -1;
                         drawInfo.Tip = string.Empty;
                     }

                     statInfoList.Add(drawInfo);
                 }

                 if (filter.Thing.MadeFromStuff)
                 {
                     DrawColumnFor(filter.Thing,
                         filter.AllowedStuffs.Count > 0
                             ? filter.AllowedStuffs.First()
                             : GenStuff.AllowedStuffsFor(filter.Thing).First(), qualityPreview);
                     if(stuffPreview is not null)
                         DrawColumnFor(filter.Thing, stuffPreview, qualityPreview);
                 }
                 else
                 {
                     DrawColumnFor(filter.Thing, null, qualityPreview);
                 }

                 
                 if (statInfoList.Count > 1)
                 {
                     // Highlight highest stat value.
                     List<StatDrawInfo> orderedList = statInfoList.OrderByDescending(t => t.Value).ToList();
                     foreach (var statDrawInfo in orderedList.Where(statDrawInfo => Mathf.Abs(statDrawInfo.Value - orderedList[0].Value) < 0.001f))
                     {
                         statDrawInfo.Color = Color.green;
                     }
                 }

                     // Draw stat for each stuff choice.
                 Text.Anchor = TextAnchor.MiddleCenter;
                 var drawRect = startRect.PopTopPartPixels(GenUI.ListSpacing).LeftHalf();

                 foreach (StatDrawInfo info in statInfoList)
                 {
                     GUI.color = info.Color;
                     Widgets.Label(
                         drawRect,
                         info.Value == -1
                             ? "-"
                             : stats[i].Worker.ValueToString(info.Value, true, stats[i].toStringNumberSense));
                     Widgets.DrawHighlightIfMouseover(drawRect);
                     Text.Anchor = TextAnchor.MiddleLeft;
                     TooltipHandler.TipRegion(drawRect, info.Tip);
                     Text.Anchor = TextAnchor.MiddleCenter;
                     
                     drawRect.x += drawRect.width;
                 }
             }

             Text.Anchor = TextAnchor.UpperLeft;
             GUI.color = Color.white;
         }
         private static T GetCachedValue<T>(Dictionary<Tuple<ThingDef, ThingDef, QualityCategory>, Dictionary<StatDef, T>> cache, Func<T> valueGetter, Tuple<ThingDef, ThingDef, QualityCategory> pair, StatDef statDef)
         {
             if (cache.TryGetValue(pair, out Dictionary<StatDef, T> cachedValues))
             {
                 if (cachedValues.TryGetValue(statDef, out T cacheValue))
                 {
                     return cacheValue;
                 }
                 cacheValue = valueGetter();
                 cachedValues[statDef] = cacheValue;
                 return cacheValue;
             }
             T statValue = valueGetter();
             cachedValues = new Dictionary<StatDef, T>();
     
             cache[pair] = cachedValues;
             cachedValues[statDef] = statValue;
     
             return statValue;
         }

        private static void DrawStatNameColumn(Rect cell, List<StatDef> stats)
        {
            foreach (StatDef statDef in stats)
            {
                Widgets.NoneLabelCenteredVertically(cell, statDef.LabelCap);
                Widgets.DrawHighlightIfMouseover(cell);
                TooltipHandler.TipRegion(cell, statDef.description);
                cell.y += GenUI.ListSpacing;
            }
        }

        private void DrawStatTableHeader(Rect startRect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = false;

            void DrawHeaderFor(Rect rect, ThingDef def, ThingDef stuff, QualityCategory quality)
            {
                Rect buttonRect = rect.PopRightPartPixels(GenUI.SmallIconSize);
                if (Widgets.ButtonImage(buttonRect.ContractedBy(1f), TexButton.Info))
                {
                    Thing thing = Utility.MakeThingWithoutID(def, stuff, quality);
                    Find.WindowStack.Add(new Dialog_InfoCard(thing));
                }

                string text = stuff != null
                    ? stuff.LabelAsStuff.CapitalizeFirst()
                    : def.LabelCap.ToString();

                rect.width -= GenUI.GapTiny;
                if (text.StripTags().GetWidthCached() > rect.width)
                    Text.Anchor = TextAnchor.MiddleLeft;

                Widgets.Label(rect, text);
            }

            if (filter.Thing.MadeFromStuff)
            {
                var stuff = filter.AllowedStuffs.Any()
                    ? filter.AllowedStuffs.First()
                    : GenStuff.AllowedStuffsFor(filter.Thing).First();

                DrawHeaderFor(startRect.LeftHalf(), filter.Thing, stuff, qualityPreview);

                if (stuffPreview != null)
                    DrawHeaderFor(startRect.RightHalf(), filter.Thing, stuffPreview, qualityPreview);
            }
            else
            {
                DrawHeaderFor(startRect.LeftHalf(), filter.Thing, null, qualityPreview);
            }
            
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }

        private void DrawStats(Rect rect)
        {
            List<StatDef> stats = new List<StatDef>();
            if (filter.Thing.IsApparel) stats = armorStats;
            else if (filter.Thing.IsMeleeWeapon) stats = baseWeaponStats.Union(meleeWeaponStats).ToList();
            else if (filter.Thing.IsRangedWeapon) stats = baseWeaponStats;
            else stats = generalItemStats;

            statScrollViewHeight = GenUI.ListSpacing * stats.Count;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, statScrollViewHeight);

            Widgets.BeginScrollView(rect, ref statScrollPosition, viewRect);

            var headerRect = viewRect.PopTopPartPixels(GenUI.ListSpacing);
            headerRect.AdjHorzBy(rect.width / 3.0f);
            var nameColumnRect = viewRect.PopLeftPartPixels(rect.width / 3.0f);
            var statColumnRect = viewRect;
            
            DrawStatTableHeader(headerRect);
            DrawStatNameColumn(nameColumnRect.TopPartPixels(GenUI.ListSpacing), stats);
            DrawStatRows(stats, statColumnRect);
            
            Widgets.EndScrollView();


            Text.Anchor = TextAnchor.UpperLeft;
        }

        private class StatDrawInfo
        {
            public Color Color = Color.white;
            public float Value = -1;
            public StatRequest StatRequest = default;
            public string Tip = string.Empty;
        }
    }
}