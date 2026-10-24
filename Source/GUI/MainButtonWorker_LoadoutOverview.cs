using RimWorld;

namespace Inventory {

    // The overview's button is only shown if enabled in the mod settings. Vanilla still handles its hotkey while the
    // button is hidden, as that only checks whether the button is disabled.
    public class MainButtonWorker_LoadoutOverview : MainButtonWorker_ToggleTab {

        public override bool Visible => ModBase.settings.showOverviewButton && base.Visible;

    }

}
