using RimWorld;
using Verse;

namespace Inventory {

    public class LoadoutState : IExposable, ILoadReferenceable {

        public string name;
        public int id = -1;

        public bool IsDefault => id == 0;

        // for creating instances when loading/saving
        public LoadoutState() { }

        public LoadoutState(string name) {
            this.name = name;
            this.id = LoadoutManager.GetNextStateId();
        }

        public override bool Equals(object obj) {
            return (obj is LoadoutState ls && ls.name == name && ls.id == id) || (obj is string s && s == name);
        }

        public override int GetHashCode() {
            return name.GetHashCode();
        }

        public string GetUniqueLoadID() {
            return "LoadoutState-" + id;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref id, nameof(id));
            Scribe_Values.Look(ref name, nameof(name));
        }

    }

}