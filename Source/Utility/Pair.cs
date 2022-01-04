using Verse;

namespace Inventory {

    class Pair<T, V> {

        private T first;
        private V second;

        public T First => first;
        public V Second => second;

        public Pair(T f, V s) {
            this.first = f;
            this.second = s;
        }

        public Pair() {
            this.first = default(T);
            this.second = default(V);
        }

        public void SetFirst(T val) => first = val;
        public void SetSecond(V val) => second = val;

        public void Deconstruct(out T first, out V second) {
            first = this.First;
            second = this.Second;
        }
    }

}