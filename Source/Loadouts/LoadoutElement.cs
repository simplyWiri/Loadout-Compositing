using System.Collections.Generic;
using Verse;

namespace Inventory {
    
    public enum ActiveCondition : byte {
        StateInactive = 0,
        StateActive = 1
    }

    public class LoadoutElement : IExposable {

        private Tag tag;
        internal LoadoutState state;
        // if switchValue is true, this element is only active when `currentState` == state, 
        // otherwise, it is only active when `currentState` != state
        private ActiveCondition activeCondition;

        public Tag Tag => tag;
        public LoadoutState State => state;
        public ActiveCondition ActiveCondition => activeCondition;

        public string StateName => State is null ? Strings.DefaultStateNameInUse : (ActiveCondition == ActiveCondition.StateActive ? "" : $"{Strings.Not} ") + State.name;
        
        public bool Active(LoadoutState currentState) {
            if (state == null) return true;

            switch (activeCondition) {
                case ActiveCondition.StateActive: return Equals(currentState, state);
                case ActiveCondition.StateInactive: return !Equals(currentState, state);
            }

            // unreachable
            return false;
        }

        public void SetLoadoutState(LoadoutState state, ActiveCondition activeCondition) {
            this.state = state;
            this.activeCondition = activeCondition;
        }

        // for creating instances when loading/saving
        public LoadoutElement() {
            
        }
        public LoadoutElement(Tag tag, LoadoutState state, ActiveCondition activeCondition = ActiveCondition.StateActive) {
            this.tag = tag;
            this.state = state;
            this.activeCondition = activeCondition;
        }
        
        public void ExposeData() {
            Scribe_References.Look(ref tag, nameof(tag));
            Scribe_References.Look(ref state, nameof(state));
            Scribe_Values.Look(ref activeCondition, nameof(activeCondition));
            
            if (Scribe.mode == LoadSaveMode.LoadingVars) {
                bool? switchValue = null;
                Scribe_Values.Look(ref switchValue, nameof(switchValue));
                
                if (switchValue is not null) {
                    activeCondition = switchValue.Value ? ActiveCondition.StateActive : ActiveCondition.StateInactive;
                }
            }
        }
    }

}