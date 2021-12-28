using System.Collections.Generic;
using Verse;

namespace Inventory {

    public class LoadoutElement : IExposable {

        private Tag tag;
        internal LoadoutState state;
        // if switchValue is true, this element is only active when `currentState` == state, 
        // otherwise, it is only active when `currentState` != state
        private bool switchValue;

        public Tag Tag => tag;

        public bool Active(LoadoutState currentState) {
            if (switchValue && Equals(currentState, state)) {
                return true;
            }

            return !switchValue && !Equals(currentState, state);
        }

        // for creating instances when loading/saving
        public LoadoutElement() {
            
        }
        public LoadoutElement(Tag tag, LoadoutState state, bool switchValue = true) {
            this.tag = tag;
            this.state = state;
            this.switchValue = switchValue;
        }
        
        public void ExposeData() {
            Scribe_References.Look(ref tag, nameof(tag));
            Scribe_References.Look(ref state, nameof(state));
            Scribe_Values.Look(ref switchValue, nameof(switchValue));
        }
    }

}