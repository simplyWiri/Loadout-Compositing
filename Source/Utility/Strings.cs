using Verse;

namespace Inventory {

    public static class Strings {

        // P = Prefix
        public const string P = "CompositableLoadouts.";
        public const string S = "Settings.";

        public static string TagLoadoutComposition => $"{P}TagLoadoutComposition".TranslateSimple();
        public static string EditXLoadout(string pawnName) => $"{P}EditXLoadout".Translate(pawnName);
        public static string EditOrCreateTags => $"{P}EditOrCreateTags".TranslateSimple();

        public static string Generic => $"{P}Generic".TranslateSimple();
        public static string Modify => $"{P}Modify".TranslateSimple();
        public static string Create => $"{P}Create".TranslateSimple();

        public static string PickTargetTag => $"{P}PickTargetTag".TranslateSimple();
        public static string SwitchTargetTag(string fromTag) => $"{P}SwitchTargetTag".Translate(fromTag);
        public static string CopyFromTag(string defName) => $"{P}CopyFromTag".Translate(defName);
        public static string NumTagsDesc => $"{P}NumTagsDesc".TranslateSimple();
        public static string ExtraCopiesDesc => $"{P}ExtraCopiesDesc".TranslateSimple();
        public static string ItemRepetitionDesc => $"{P}ItemRepetitionDesc".TranslateSimple();

        public static string PickUpItems(string itemTag, string count) => $"{P}PickUpItems".Translate(itemTag, count);

        public static string SelectTag => $"{P}SelectTag".TranslateSimple();

        public static string NoTagsYetWarning => $"{P}NoTagsYetWarning".TranslateSimple();
        public static string CreateNewTag => $"{P}CreateNewTag".TranslateSimple();
        public static string DeleteTag => $"{P}DeleteTag".TranslateSimple();

        public static string ChangeTagName => $"{P}ChangeTagName".TranslateSimple();
        public static string CopyPasteExplain => $"{P}CopyPasteExplain".TranslateSimple();

        public static string SpecifyElementsToolTip => $"{P}SpecifyElementsToolTip".TranslateSimple();
        public static string EditQuantity => $"{P}EditQuantity".TranslateSimple();
        public static string RemoveItemFromTag => $"{P}RemoveItemFromTag".TranslateSimple();

        public static string SelectPrevious => $"{P}SelectPrevious".TranslateSimple();
        public static string SelectNext => $"{P}SelectNext".TranslateSimple();
        public static string TopFourSkills => $"{P}TopFourSkills".TranslateSimple();
        public static string ApparelWhichCanBeWorn => $"{P}ApparelWhichCanBeWorn".TranslateSimple();
        public static string AppliedTags => $"{P}AppliedTags".TranslateSimple();
        public static string PawnStats => $"{P}PawnStats".TranslateSimple();
        public static string ShowCoverage => $"{P}ShowCoverage".TranslateSimple();
        public static string HideCoverage => $"{P}HideCoverage".TranslateSimple();
        public static string LoadoutStatistics => $"{P}LoadoutStatistics".TranslateSimple();
        public static string Weight => $"{P}Weight".TranslateSimple();
        public static string WeightOverCapacity => $"{P}WeightOverCapacity".TranslateSimple();

        // intentionally not translated
        public static string HitPointsAmount => $"{P}HitPointsAmount";

        public static string Customize => $"{P}Customize".TranslateSimple();
        public static string MouseOverDetails => $"{P}MouseOverDetails".TranslateSimple();
        public static string Statistics => $"{P}Statistics".TranslateSimple();
        public static string StuffFilter => $"{P}StuffFilter".TranslateSimple();
        public static string NoMaterial => $"{P}NoMaterial".TranslateSimple();
        public static string AddTag => $"{P}AddTag".TranslateSimple();
        public static string PreviewQuality => $"{P}PreviewQuality".TranslateSimple();

        public static string CoverageExplanation => $"{P}CoverageExplanation".TranslateSimple();

        public static string ModifyStates => $"{P}ModifyStates".TranslateSimple();
        public static string SelectState => $"{P}SelectState".TranslateSimple();
        public static string DefaultStateName => $"{P}DefaultStateName".TranslateSimple();
        public static string DefaultStateNameInUse => $"{P}DefaultStateNameInUse".TranslateSimple();

        public static string Map => $"{P}Map".TranslateSimple();
        public static string Caravan => $"{P}Caravan".TranslateSimple();
        public static string MassChangeStates(Pawn pawn, string area, LoadoutState state) => $"{P}MassChangeStates".Translate(pawn.LabelShortCap, area, state?.name ?? DefaultStateName);
        public static string StatesExplanation => $"{P}StatesExplanation".TranslateSimple();
        public static string EditLoadoutDesc(Pawn p) => $"{P}EditLoadoutDesc".Translate(p.LabelShortCap);

        public static string SatisfyLoadoutNow => $"{P}SatisfyLoadoutNow".TranslateSimple();
        public static string SatisfyLoadoutNowFail(ThingWithComps p) => $"{P}SatisfyLoadoutNowFail".Translate(p.LabelCap);

        public static string Active => $"{P}Active".TranslateSimple();
        public static string Inactive => $"{P}Inactive".TranslateSimple();
        public static string Is => $"{P}Is".TranslateSimple();
        public static string StateActive => $"{P}StateActive".TranslateSimple();
        public static string ActiveWhen => $"{P}ActiveWhen".TranslateSimple();

        public static string ViewAsIf => $"{P}ViewAsIf".TranslateSimple();
        public static string WereActive => $"{P}WereActive".TranslateSimple();
        public static string SetDefault => $"{P}SetDefault".TranslateSimple();
        public static string CouldNotFindPawn => $"{P}CouldNotFindPawn".TranslateSimple();

        public static string ImmediatelyResolveLoadout => $"{P}{S}ImmediatelyResolveLoadout".TranslateSimple();
        public static string ImmediatelyResolveLoadoutDesc => $"{P}{S}ImmediatelyResolveLoadoutDesc".TranslateSimple();
        public static string BiasLoadBearingItems => $"{P}{S}BiasLoadBearingItems".TranslateSimple();
        public static string ChangeDefaults => $"{P}{S}ChangeDefaults".TranslateSimple();
        public static string OnlyLoadoutItems => $"{P}{S}OnlyLoadoutItems".TranslateSimple();


    }

}